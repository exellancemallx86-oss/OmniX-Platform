import { useEffect, useState } from 'react';

const API = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000/api';
const auth = () => ({ 'Content-Type': 'application/json',
  Authorization: `Bearer ${localStorage.getItem('mx_token')}` });

const fmt  = (n: number) => n?.toLocaleString('ar-EG') ?? '0';
const fmtC = (n: number) => `${fmt(n)} ج.م`;

interface Settlement {
  storeId: string; storeName: string; orderCount: number;
  grossRevenue: number; commissionTotal: number; netPayable: number;
  period: string; status: string; generatedAt: string;
}

const MONTHS = [
  'يناير','فبراير','مارس','أبريل','مايو','يونيو',
  'يوليو','أغسطس','سبتمبر','أكتوبر','نوفمبر','ديسمبر'
];

export default function SettlementsPage() {
  const now  = new Date();
  const [year,  setYear]        = useState(now.getFullYear());
  const [month, setMonth]       = useState(now.getMonth()); // 0-indexed
  const [data,  setData]        = useState<Settlement[]>([]);
  const [loading, setLoading]   = useState(false);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [exporting, setExp]     = useState(false);
  const [paying,    setPaying]  = useState<string|null>(null);

  const load = async () => {
    setLoading(true);
    try {
      const r = await fetch(
        `${API}/mall/admin/analytics?period=month`, { headers: auth() });
      const d = await r.json();
      // Build settlement from top stores (real API: /api/mall/admin/settlements)
      const stores: Settlement[] = (d.data?.topStores ?? []).map((s: any) => ({
        storeId:         s.storeId,
        storeName:       s.storeName,
        orderCount:      s.orders,
        grossRevenue:    s.revenue,
        commissionTotal: s.commission,
        netPayable:      s.revenue - s.commission,
        period:          `${MONTHS[month]} ${year}`,
        status:          'Pending',
        generatedAt:     new Date().toISOString(),
      }));
      setData(stores);
    } catch {}
    setLoading(false);
  };

  useEffect(() => { load(); }, [year, month]);

  const toggleSelect = (id: string) => {
    setSelected(prev => {
      const next = new Set(prev);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  };

  const selectAll   = () => setSelected(new Set(data.map(d => d.storeId)));
  const deselectAll = () => setSelected(new Set());

  const exportCsv = async () => {
    setExp(true);
    const from = new Date(year, month, 1).toISOString().slice(0,10);
    const to   = new Date(year, month+1, 0).toISOString().slice(0,10);
    const url  = `${API}/mall/admin/export/commissions?from=${from}&to=${to}`;
    const r    = await fetch(url, { headers: auth() });
    const blob = await r.blob();
    const link = document.createElement('a');
    link.href  = URL.createObjectURL(blob);
    link.download = `commissions_${MONTHS[month]}_${year}.csv`;
    link.click();
    setExp(false);
  };

  const markPaid = async (storeId: string) => {
    setPaying(storeId);
    await new Promise(r => setTimeout(r, 800)); // simulated
    setData(prev => prev.map(d =>
      d.storeId === storeId ? {...d, status:'Paid'} : d));
    setSelected(prev => { const n = new Set(prev); n.delete(storeId); return n; });
    setPaying(null);
  };

  const markAllPaid = async () => {
    for (const id of selected) await markPaid(id);
  };

  const totals = data.reduce((a, d) => ({
    orders:     a.orders     + d.orderCount,
    gross:      a.gross      + d.grossRevenue,
    commission: a.commission + d.commissionTotal,
    net:        a.net        + d.netPayable,
  }), { orders: 0, gross: 0, commission: 0, net: 0 });

  const pendingData = data.filter(d => d.status === 'Pending');
  const paidData    = data.filter(d => d.status === 'Paid');

  return (
    <div className="min-h-screen p-6 max-w-7xl mx-auto" dir="rtl">

      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-4 mb-8">
        <div>
          <h1 className="text-2xl font-black text-slate-100">💳 تسويات العمولات</h1>
          <p className="text-slate-400 text-sm mt-1">
            {MONTHS[month]} {year} — {data.length} محل
          </p>
        </div>
        <div className="flex gap-3">
          <button onClick={exportCsv} disabled={exporting}
            className="flex items-center gap-2 px-4 py-2 border border-slate-700 rounded-lg text-slate-300 text-sm hover:border-slate-500 disabled:opacity-40">
            {exporting ? '⏳' : '⬇️'} تصدير CSV
          </button>
          {selected.size > 0 && (
            <button onClick={markAllPaid}
              className="px-4 py-2 bg-green-600 hover:bg-green-700 text-white rounded-lg text-sm font-bold">
              ✅ دفع المحلات المختارة ({selected.size})
            </button>
          )}
        </div>
      </div>

      {/* Month selector */}
      <div className="card mb-6">
        <div className="flex flex-wrap gap-3 items-center">
          <div className="flex items-center gap-2">
            <button onClick={() => setYear(y => y-1)}
              className="w-8 h-8 rounded-lg border border-slate-700 text-slate-400 hover:text-slate-200">‹</button>
            <span className="text-slate-200 font-bold w-12 text-center">{year}</span>
            <button onClick={() => setYear(y => y+1)}
              disabled={year >= now.getFullYear()}
              className="w-8 h-8 rounded-lg border border-slate-700 text-slate-400 hover:text-slate-200 disabled:opacity-30">›</button>
          </div>
          <div className="flex flex-wrap gap-1">
            {MONTHS.map((m, i) => (
              <button key={i} onClick={() => setMonth(i)}
                className={`px-3 py-1 rounded-lg text-xs font-semibold transition-all ${
                  month === i ? 'bg-blue-600 text-white' : 'text-slate-400 border border-slate-700 hover:border-slate-500'
                }`}>
                {m}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* Summary KPIs */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        {[
          { label:'إجمالي المبيعات', val:fmtC(totals.gross),      color:'#3b82f6', icon:'💰' },
          { label:'العمولة المستحقة',val:fmtC(totals.commission), color:'#10b981', icon:'📊' },
          { label:'المستحق للمحلات', val:fmtC(totals.net),        color:'#f59e0b', icon:'🏪' },
          { label:'الطلبات المكتملة', val:fmt(totals.orders),     color:'#8b5cf6', icon:'📦' },
        ].map(({ label, val, color, icon }) => (
          <div key={label} className="card">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-slate-400 text-xs">{label}</p>
                <p className="font-bold text-lg mt-1" style={{color}}>{val}</p>
              </div>
              <span className="text-2xl">{icon}</span>
            </div>
          </div>
        ))}
      </div>

      {/* Pending Settlements */}
      {loading ? (
        <div className="card text-center py-12 text-slate-400">⏳ جاري التحميل...</div>
      ) : (
        <>
          <div className="card overflow-x-auto mb-4">
            <div className="flex items-center justify-between mb-4">
              <h3 className="font-bold text-slate-200">
                ⏳ مستحق الدفع ({pendingData.length})
              </h3>
              <div className="flex gap-2">
                <button onClick={selectAll}
                  className="text-xs text-blue-400 hover:text-blue-300">اختيار الكل</button>
                <span className="text-slate-600">|</span>
                <button onClick={deselectAll}
                  className="text-xs text-slate-400 hover:text-slate-300">إلغاء الاختيار</button>
              </div>
            </div>

            <table className="w-full text-sm">
              <thead>
                <tr className="text-slate-500 text-xs uppercase border-b border-slate-800">
                  <th className="pb-2 w-8"><input type="checkbox"
                    checked={selected.size === pendingData.length && pendingData.length > 0}
                    onChange={e => e.target.checked ? selectAll() : deselectAll()}
                    className="accent-blue-500"/></th>
                  <th className="pb-2 text-right">المحل</th>
                  <th className="pb-2">الطلبات</th>
                  <th className="pb-2">الإيرادات</th>
                  <th className="pb-2">العمولة</th>
                  <th className="pb-2">المستحق</th>
                  <th className="pb-2">إجراء</th>
                </tr>
              </thead>
              <tbody>
                {pendingData.map(s => (
                  <tr key={s.storeId}
                    className={`border-b border-slate-800 transition-colors ${
                      selected.has(s.storeId) ? 'bg-blue-500/5' : 'hover:bg-slate-800/30'
                    }`}>
                    <td className="py-3 text-center">
                      <input type="checkbox"
                        checked={selected.has(s.storeId)}
                        onChange={() => toggleSelect(s.storeId)}
                        className="accent-blue-500"/>
                    </td>
                    <td className="py-3">
                      <span className="font-semibold text-slate-200">{s.storeName}</span>
                    </td>
                    <td className="py-3 text-center text-slate-400">{s.orderCount}</td>
                    <td className="py-3 text-center text-blue-400">{fmtC(s.grossRevenue)}</td>
                    <td className="py-3 text-center text-green-400">{fmtC(s.commissionTotal)}</td>
                    <td className="py-3 text-center font-bold text-yellow-400">{fmtC(s.netPayable)}</td>
                    <td className="py-3 text-center">
                      <button onClick={() => markPaid(s.storeId)}
                        disabled={paying === s.storeId}
                        className="px-3 py-1 bg-green-600/20 hover:bg-green-600/40 border border-green-500/40 text-green-400 rounded-lg text-xs font-bold disabled:opacity-40">
                        {paying === s.storeId ? '⏳' : '✓ دفع'}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
              {pendingData.length > 0 && (
                <tfoot>
                  <tr className="border-t-2 border-slate-700 text-sm font-bold">
                    <td colSpan={2} className="pt-3 text-slate-300">الإجمالي</td>
                    <td className="pt-3 text-center text-slate-300">{totals.orders}</td>
                    <td className="pt-3 text-center text-blue-400">{fmtC(totals.gross)}</td>
                    <td className="pt-3 text-center text-green-400">{fmtC(totals.commission)}</td>
                    <td className="pt-3 text-center text-yellow-400">{fmtC(totals.net)}</td>
                    <td/>
                  </tr>
                </tfoot>
              )}
            </table>
            {pendingData.length === 0 && (
              <div className="text-center py-10 text-slate-500">
                ✅ لا توجد تسويات معلقة لهذا الشهر
              </div>
            )}
          </div>

          {/* Paid */}
          {paidData.length > 0 && (
            <div className="card overflow-x-auto">
              <h3 className="font-bold text-slate-200 mb-4">✅ تم الدفع ({paidData.length})</h3>
              <div className="space-y-2">
                {paidData.map(s => (
                  <div key={s.storeId}
                    className="flex items-center justify-between p-3 bg-green-500/5 border border-green-500/20 rounded-lg">
                    <span className="text-slate-200 font-semibold">{s.storeName}</span>
                    <div className="flex items-center gap-4 text-sm">
                      <span className="text-slate-400">{s.orderCount} طلب</span>
                      <span className="text-yellow-400 font-bold">{fmtC(s.netPayable)}</span>
                      <span className="badge-green px-2 py-1 rounded text-xs font-bold">✓ مدفوع</span>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </>
      )}

      <style jsx global>{`
        .card { background:#0f172a; border:1px solid #1e293b; border-radius:14px; padding:20px; }
        .badge-green { background:rgba(16,185,129,.15); color:#10b981; }
      `}</style>
    </div>
  );
}
