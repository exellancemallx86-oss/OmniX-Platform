import { createContext, useContext, useEffect, useState, ReactNode } from 'react';

// ══════════════════════════════════════════════════════════════════════════
//  MODULE SYSTEM — Dynamic rendering based on Role + Feature Flags
//  Every admin page checks if its module is enabled before rendering
// ══════════════════════════════════════════════════════════════════════════

const API = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000/api';

interface FeatureFlag { key: string; isEnabled: boolean; name: string; category: string; }
interface PlanStatus  { planKey: string; planNameAr: string; hasAnalytics: boolean; hasAI: boolean; hasExport: boolean; upgradeMessage?: string; }
interface User        { role: string; mallId: string; name: string; }

interface ModuleContextValue {
  features:   Record<string, boolean>;
  plan:        PlanStatus | null;
  user:        User | null;
  loading:     boolean;
  canAccess:  (feature: string) => boolean;
  isRole:     (role: string) => boolean;
}

const ModuleContext = createContext<ModuleContextValue>({
  features: {}, plan: null, user: null, loading: true,
  canAccess: () => true, isRole: () => false,
});

// ─── Provider ─────────────────────────────────────────────────────────────
export function ModuleProvider({ children }: { children: ReactNode }) {
  const [features, setFeatures] = useState<Record<string, boolean>>({});
  const [plan,     setPlan]     = useState<PlanStatus | null>(null);
  const [user,     setUser]     = useState<User | null>(null);
  const [loading,  setLoading]  = useState(true);

  useEffect(() => {
    const token = localStorage.getItem('mx_token');
    if (!token) { setLoading(false); return; }
    loadAll(token);
  }, []);

  const loadAll = async (token: string) => {
    const authHeaders = { Authorization: `Bearer ${token}` };
    try {
      const [featRes, planRes] = await Promise.all([
        fetch(`${API}/mall/admin/features`, { headers: authHeaders }),
        fetch(`${API}/mall/subscription/status`, { headers: authHeaders }),
      ]);

      if (featRes.ok) {
        const fd = await featRes.json();
        const map: Record<string, boolean> = {};
        for (const f of (fd.data as FeatureFlag[] ?? []))
          map[f.key] = f.isEnabled;
        setFeatures(map);
      }

      if (planRes.ok) {
        const pd = await planRes.json();
        setPlan(pd as PlanStatus);
      }

      // Decode JWT user info
      try {
        const payload = JSON.parse(atob(token.split('.')[1]));
        setUser({
          role:   payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ?? payload.role ?? 'Customer',
          mallId: payload.mall_id ?? '',
          name:   payload.name ?? payload.email ?? '',
        });
      } catch {}
    } catch {}
    setLoading(false);
  };

  const canAccess = (feature: string): boolean => {
    if (!feature) return true;
    return features[feature] !== false; // default true if not set
  };

  const isRole = (role: string): boolean =>
    user?.role?.toLowerCase() === role.toLowerCase();

  return (
    <ModuleContext.Provider value={{ features, plan, user, loading, canAccess, isRole }}>
      {children}
    </ModuleContext.Provider>
  );
}

export const useModules = () => useContext(ModuleContext);

// ─── Module Gate Component ─────────────────────────────────────────────────
interface ModuleGateProps {
  feature?:     string;   // feature key to check
  role?:        string;   // required role
  plan?:        string;   // required plan: 'pro' | 'enterprise'
  fallback?:    ReactNode;
  children:     ReactNode;
}

export function ModuleGate({ feature, role, plan, fallback, children }: ModuleGateProps) {
  const { features, plan: planStatus, user, loading } = useModules();
  if (loading) return null;

  // Role check
  if (role && user?.role?.toLowerCase() !== role.toLowerCase()) {
    return fallback ? <>{fallback}</> : <UpgradePrompt message="ليس لديك صلاحية الوصول لهذا القسم." />;
  }

  // Feature check
  if (feature && features[feature] === false) {
    return fallback ? <>{fallback}</> : <DisabledFeaturePrompt featureKey={feature} />;
  }

  // Plan check
  if (plan === 'pro' && planStatus?.planKey === 'free') {
    return fallback ? <>{fallback}</> : <UpgradePrompt message="هذه الميزة متاحة في الخطة الاحترافية." plan="pro" />;
  }
  if (plan === 'enterprise' && !['enterprise'].includes(planStatus?.planKey ?? '')) {
    return fallback ? <>{fallback}</> : <UpgradePrompt message="هذه الميزة متاحة في خطة المؤسسات." plan="enterprise" />;
  }

  return <>{children}</>;
}

// ─── Disabled Feature Prompt ───────────────────────────────────────────────
function DisabledFeaturePrompt({ featureKey }: { featureKey: string }) {
  const labels: Record<string, string> = {
    ai_assistant:   'المساعد الذكي',
    analytics:      'التحليلات المتقدمة',
    restaurant:     'قائمة الطعام',
    booking:        'الحجوزات',
    wallet:         'المحفظة',
    loyalty:        'نقاط الولاء',
    export:         'تصدير البيانات',
    referral:       'نظام الإحالة',
    geo_fencing:    'الإشعارات الجغرافية',
    mall_map:       'خريطة المول',
    whatsapp:       'إشعارات واتساب',
  };

  return (
    <div className="gate-box gate-disabled">
      <div className="gate-icon">🔒</div>
      <h3>هذه الخدمة معطّلة</h3>
      <p>خدمة "{labels[featureKey] ?? featureKey}" غير مفعّلة في مولك حالياً.</p>
      <p style={{fontSize:12, color:'#64748b'}}>يمكن لمسؤول المول تفعيلها من إعدادات المميزات.</p>
      <style>{gateStyles}</style>
    </div>
  );
}

// ─── Upgrade Prompt ────────────────────────────────────────────────────────
function UpgradePrompt({ message, plan }: { message: string; plan?: string }) {
  return (
    <div className="gate-box gate-upgrade">
      <div className="gate-icon">🚀</div>
      <h3>رقّي خطتك</h3>
      <p>{message}</p>
      {plan && (
        <a href="/subscription/upgrade" className="gate-btn">
          رقّي الآن {plan === 'pro' ? '— 499 ج.م/شهر' : ''}
        </a>
      )}
      <style>{gateStyles}</style>
    </div>
  );
}

const gateStyles = `
  .gate-box{background:#1e293b;border:1px solid #334155;border-radius:16px;padding:40px;text-align:center;max-width:400px;margin:40px auto}
  .gate-icon{font-size:48px;margin-bottom:16px}
  .gate-box h3{font-size:18px;font-weight:800;color:#f1f5f9;margin-bottom:8px}
  .gate-box p{color:#94a3b8;font-size:14px;margin-bottom:8px}
  .gate-btn{display:inline-block;background:#3b82f6;color:#fff;padding:12px 24px;border-radius:10px;font-weight:700;margin-top:12px;text-decoration:none}
  .gate-disabled .gate-icon{filter:grayscale(1)}
`;

// ─── Feature-aware Admin Sidebar ───────────────────────────────────────────
interface SidebarItem { label: string; href: string; icon: string; feature?: string; role?: string; plan?: string; }

const SIDEBAR_ITEMS: SidebarItem[] = [
  { label:'الرئيسية',      href:'/mall-admin',                icon:'🏠' },
  { label:'التحليلات',     href:'/mall-admin/analytics',       icon:'📊', feature:'analytics' },
  { label:'الطلبات',       href:'/mall-admin/orders',           icon:'📦' },
  { label:'المحلات',       href:'/mall-admin/stores',           icon:'🏪' },
  { label:'العروض',        href:'/mall-admin/promotions',       icon:'🎟️', feature:'promotions' },
  { label:'التسويات',      href:'/mall-admin/settlements',      icon:'💳', feature:'commissions' },
  { label:'العملاء',       href:'/mall-admin/customers',        icon:'👥', feature:'customer_auth' },
  { label:'نقاط الولاء',  href:'/mall-admin/loyalty',          icon:'⭐', feature:'loyalty' },
  { label:'الإشعارات',    href:'/mall-admin/notifications',    icon:'🔔', feature:'notifications' },
  { label:'الميزات',       href:'/mall-admin/features',         icon:'🔧', role:'PlatformOwner' },
  { label:'الاشتراك',      href:'/mall-admin/subscription',    icon:'💰' },
  { label:'تصدير البيانات',href:'/mall-admin/export',          icon:'⬇️', feature:'export' },
  { label:'المساعد الذكي', href:'/mall-admin/ai',              icon:'🤖', feature:'ai_assistant' },
];

export function AdminSidebar({ currentPath }: { currentPath: string }) {
  const { features, user } = useModules();

  const visible = SIDEBAR_ITEMS.filter(item => {
    if (item.feature && features[item.feature] === false) return false;
    if (item.role && user?.role !== item.role && user?.role !== 'PlatformOwner') return false;
    return true;
  });

  return (
    <nav className="sidebar" dir="rtl">
      <div className="sidebar-brand">🏬 MallX Admin</div>
      {visible.map(item => (
        <a key={item.href} href={item.href}
          className={`sidebar-item ${currentPath === item.href ? 'active' : ''}`}>
          <span className="sidebar-icon">{item.icon}</span>
          <span>{item.label}</span>
        </a>
      ))}
      <style>{`
        .sidebar{background:#0f172a;border-left:1px solid #1e293b;width:220px;min-height:100vh;padding:16px 0;display:flex;flex-direction:column;gap:2px}
        .sidebar-brand{font-size:16px;font-weight:900;color:#f1f5f9;padding:12px 16px 20px;border-bottom:1px solid #1e293b;margin-bottom:8px}
        .sidebar-item{display:flex;align-items:center;gap:10px;padding:10px 16px;color:#94a3b8;font-size:13px;border-radius:8px;margin:0 8px;transition:all .15s}
        .sidebar-item:hover{background:#1e293b;color:#f1f5f9}
        .sidebar-item.active{background:#3b82f610;color:#3b82f6;font-weight:700}
        .sidebar-icon{font-size:15px;width:20px;text-align:center}
      `}</style>
    </nav>
  );
}
