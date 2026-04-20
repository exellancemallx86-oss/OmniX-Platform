import { useEffect, useState, useCallback } from 'react';
import {
  AreaChart, Area, BarChart, Bar,
  XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer
} from 'recharts';

const API = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000/api';
const auth = () => ({ Authorization: `Bearer ${localStorage.getItem('mx_token')}` });
const fmt  = (n: number) => n?.toLocaleString('ar-EG') ?? '0';
const fmtC = (n: number) => `${fmt(n)} ج.م`;
const pct  = (n: number) => `${n >= 0 ? '+' : ''}${n?.toFixed(1)}%`;

export default function MallAdminDashboard() {
  const [data,   setData]   = useState<any>(null);
  const [period, setPeriod] = useState('month');
  const [loading,setLoading]= useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const r = await fetch(`${API}/mall/admin/analytics?period=${period}`, { headers: auth() });
      const d = await r.json();
      setData(d.data);
    } catch {}
    setLoading(false);
  }, [period]);

  useEffect(() => { load(); }, [load]);

  const kpis = [
    { label:'الإيرادات',        val: fmtC(data?.revenue?.total ?? 0),          grow: data?.revenue?.growthPct,  icon:'💰', color:'#3b82f6' },
    { label:'عمولة المول',      val: fmtC(data?.revenue?.totalCommission ?? 0), icon:'📊',                      color:'#10b981' },
    { label:'الطلبات',          val: fmt(data?.orders?.total ?? 0),             grow: data?.orders?.growthPct,   icon:'📦', color:'#8b5cf6' },
    { label:'نسبة الإتمام',     val: `${data?.orders?.successRate ?? 0}%`,       icon:'✅',                      color:'#10b981' },
    { label:'العملاء النشطون',  val: fmt(data?.customers?.totalActive ?? 0),     icon:'👥',                      color:'#f59e0b' },
    { label:'جدد هذه الفترة',   val: fmt(data?.customers?.newThisPeriod ?? 0),   icon:'🆕',                      color:'#3b82f6' },
    { label:'متوسط الطلب',      val: fmtC(data?.revenue?.avgOrderValue ?? 0),    icon:'🧾',                      color:'#f59e0b' },
    { label:'نقاط صُدرت',       val: fmt(data?.loyalty?.pointsIssued ?? 0),      icon:'⭐',                      color:'#f59e0b' },
  ];

  const chartData = (data?.revenueChart ?? []).map((d: any) => ({
    name: new Date(d.date).toLocaleDateString('ar-EG', {day:'numeric', month:'short'}),
    revenue: d.value,
    orders:  d.count,
  }));

  const hourlyData = (data?.hourlySales ?? []).map((h: any) => ({
    name: `${h.hour}:00`, revenue: h.revenue, orders: h.orders,
  }));

  return (
    <div className="min-h-screen p-6 max-w-7xl mx-auto" dir="rtl">

      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-4 mb-8">
        <div>
          <h1 className="text-2xl font-black text-slate-100">🏬 لوحة إدارة المول</h1>
          <p className="text-slate-400 text-sm mt-1">{data?.period ?? '—'}</p>
        </div>
        {/* Period buttons */}
        <div className="flex gap-2 bg-slate-900 p-1 rounded-xl border border-slate-800">
          {[['today','اليوم'],['week','أسبوع'],['month','شهر'],['quarter','ربع'],['year','سنة']].map(([v,l]) => (
            <button key={v} onClick={() => setPeriod(v)}
              className={`px-4 py-2 rounded-lg text-sm font-semibold transition-all ${
                period === v ? 'bg-blue-600 text-white' : 'text-slate-400 hover:text-slate-200'
              }`}>{l}</button>
          ))}
        </div>
      </div>

      {loading ? (
        <div className="text-center text-slate-400 py-20 animate-pulse">⏳ جاري تحميل البيانات...</div>
      ) : (
        <>
          {/* KPI Grid */}
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
            {kpis.map(({ label, val, grow, icon, color }) => (
              <div key={label} className="card">
                <div className="flex items-start justify-between mb-2">
                  <span className="text-2xl">{icon}</span>
                  {grow !== undefined && (
                    <span className={`text-xs font-bold px-2 py-0.5 rounded-full ${
                      grow >= 0 ? 'text-green-400 bg-green-400/10' : 'text-red-400 bg-red-400/10'
                    }`}>{pct(grow)}</span>
                  )}
                </div>
                <p className="text-xl font-black" style={{color}}>{val}</p>
                <p className="text-slate-400 text-xs mt-1">{label}</p>
              </div>
            ))}
          </div>

          {/* Charts Row */}
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-8">

            {/* Revenue Area Chart */}
            <div className="card">
              <h3 className="font-bold text-slate-200 mb-4">📈 تطور الإيرادات</h3>
              <ResponsiveContainer width="100%" height={200}>
                <AreaChart data={chartData}>
                  <defs>
                    <linearGradient id="gRev" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%"  stopColor="#3b82f6" stopOpacity={0.25}/>
                      <stop offset="95%" stopColor="#3b82f6" stopOpacity={0}/>
                    </linearGradient>
                  </defs>
                  <CartesianGrid strokeDasharray="3 3" stroke="#1e293b"/>
                  <XAxis dataKey="name" tick={{fill:'#64748b', fontSize:9}}/>
                  <YAxis tick={{fill:'#64748b', fontSize:9}}/>
                  <Tooltip contentStyle={{background:'#1e293b',border:'1px solid #334155',fontSize:11}}
                    formatter={(v: number) => [fmtC(v),'الإيرادات']}/>
                  <Area type="monotone" dataKey="revenue" stroke="#3b82f6"
                    fill="url(#gRev)" strokeWidth={2}/>
                </AreaChart>
              </ResponsiveContainer>
            </div>

            {/* Hourly Bar Chart */}
            <div className="card">
              <h3 className="font-bold text-slate-200 mb-4">🕐 توزيع المبيعات بالساعة</h3>
              <ResponsiveContainer width="100%" height={200}>
                <BarChart data={hourlyData}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#1e293b"/>
                  <XAxis dataKey="name" tick={{fill:'#64748b', fontSize:8}}/>
                  <YAxis tick={{fill:'#64748b', fontSize:9}}/>
                  <Tooltip contentStyle={{background:'#1e293b',border:'1px solid #334155',fontSize:11}}
                    formatter={(v: number) => [fmtC(v)]}/>
                  <Bar dataKey="revenue" fill="#3b82f6" radius={[3,3,0,0]}/>
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>

          {/* Fulfillment + Payment breakdown */}
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-8">
            {[
              { title:'🚗 طريقة الاستلام', data: data?.orders?.byFulfillmentType },
              { title:'💳 طريقة الدفع',   data: data?.orders?.byPaymentMethod   },
            ].map(({ title, data: d }) => (
              <div key={title} className="card">
                <h3 className="font-bold text-slate-200 mb-4">{title}</h3>
                {Object.entries(d ?? {}).map(([k, v]) => {
                  const total = Object.values(d ?? {}).reduce((a: number, b: any) => a + b, 0) as number;
                  const pct = total > 0 ? ((v as number) / total * 100).toFixed(0) : 0;
                  return (
                    <div key={k} className="mb-3">
                      <div className="flex justify-between text-xs text-slate-400 mb-1">
                        <span>{k === 'Delivery' ? 'توصيل' : k === 'Pickup' ? 'استلام' : k}</span>
                        <span>{v as number} ({pct}%)</span>
                      </div>
                      <div className="w-full bg-slate-800 rounded-full h-2">
                        <div className="h-2 rounded-full bg-blue-500"
                          style={{width:`${pct}%`}}/>
                      </div>
                    </div>
                  );
                })}
              </div>
            ))}
          </div>

          {/* Top Stores Table */}
          {(data?.topStores?.length ?? 0) > 0 && (
            <div className="card overflow-x-auto mb-8">
              <h3 className="font-bold text-slate-200 mb-4">🏪 أفضل المتاجر أداءً</h3>
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-slate-500 text-xs uppercase border-b border-slate-800">
                    <th className="pb-2 text-right">#</th>
                    <th className="pb-2 text-right">المتجر</th>
                    <th className="pb-2">الطلبات</th>
                    <th className="pb-2">الإيرادات</th>
                    <th className="pb-2">العمولة</th>
                  </tr>
                </thead>
                <tbody>
                  {data.topStores.slice(0, 8).map((s: any, i: number) => (
                    <tr key={s.storeId} className="border-b border-slate-800 hover:bg-slate-800/30">
                      <td className="py-2 text-slate-500 font-bold">{i + 1}</td>
                      <td className="py-2">
                        <div className="font-semibold text-slate-200">{s.storeName}</div>
                        <div className="text-xs text-slate-500">{s.storeType}</div>
                      </td>
                      <td className="py-2 text-center text-slate-300">{s.orders}</td>
                      <td className="py-2 text-center font-semibold text-blue-400">{fmtC(s.revenue)}</td>
                      <td className="py-2 text-center text-green-400">{fmtC(s.commission)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* Quick links */}
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
            {[
              ['📊 التقارير المفصلة', '/mall-admin/analytics'],
              ['💳 التسويات',         '/mall-admin/settlements'],
              ['📣 العروض',           '/mall-admin/promotions'],
              ['📥 تصدير البيانات',   `/api/mall/admin/export/orders?from=${new Date(Date.now()-30*86400*1000).toISOString().slice(0,10)}&to=${new Date().toISOString().slice(0,10)}`],
            ].map(([label, href]) => (
              <a key={label as string} href={href as string}
                className="card text-center hover:border-blue-500/50 transition-colors cursor-pointer text-slate-300 hover:text-blue-400 font-semibold text-sm">
                {label}
              </a>
            ))}
          </div>
        </>
      )}

      <style jsx global>{`
        .card { background:#0f172a; border:1px solid #1e293b; border-radius:14px; padding:20px; }
      `}</style>
    </div>
  );
}
