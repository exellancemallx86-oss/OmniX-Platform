import { useEffect, useState } from 'react';
import Head from 'next/head';
import Link from 'next/link';

const API = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000/api';

interface Product { id: string; name: string; price: number; storeId: string; storeName: string; imageUrl?: string; avgRating?: number; }
interface Category { id: string; name: string; icon: string; productCount: number; }
interface Store { id: string; name: string; storeType: string; avgRating?: number; logoUrl?: string; }

const MALL_SLUG = process.env.NEXT_PUBLIC_MALL_SLUG ?? 'mallx-demo';

async function getMallId(): Promise<string> {
  try {
    const r = await fetch(`${API}/mall/resolve/${MALL_SLUG}`);
    const d = await r.json();
    return d.data?.id ?? '';
  } catch { return ''; }
}

// ══════════════════════════════════════════════════════════════════════════
//  PUBLIC MARKETPLACE HOMEPAGE
//  Browsing without login — auth gate only on Buy/Checkout
// ══════════════════════════════════════════════════════════════════════════
export default function MarketplacePage() {
  const [mallId, setMallId]     = useState('');
  const [products, setProducts] = useState<Product[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [stores, setStores]     = useState<Store[]>([]);
  const [trending, setTrending] = useState<Product[]>([]);
  const [flash, setFlash]       = useState<any[]>([]);
  const [search, setSearch]     = useState('');
  const [catFilter, setCat]     = useState('all');
  const [loading, setLoading]   = useState(true);
  const [token]                 = useState(() =>
    typeof window !== 'undefined' ? localStorage.getItem('mx_token') : null);

  useEffect(() => {
    getMallId().then(id => {
      setMallId(id);
      loadAll(id);
    });
  }, []);

  const loadAll = async (id: string) => {
    if (!id) return;
    setLoading(true);
    try {
      const [homeRes, promoRes] = await Promise.all([
        fetch(`${API}/mall/${id}`),
        fetch(`${API}/mall/promotions?mallId=${id}`),
      ]);
      const home  = await homeRes.json();
      const promo = await promoRes.json();
      const d = home.data;
      setCategories(d?.categories ?? []);
      setStores(d?.featured ?? []);
      setProducts([
        ...(d?.restaurants ?? []),
        ...(d?.retail     ?? []),
        ...(d?.services   ?? []),
      ].flatMap(s => (s.products ?? [])));
      setTrending(d?.trending ?? []);
      setFlash(promo.data?.flashSales ?? []);
    } catch {}
    setLoading(false);
  };

  const filtered = catFilter === 'all'
    ? products
    : products.filter(p => (p as any).storeType === catFilter);

  const searched = search
    ? filtered.filter(p => p.name.toLowerCase().includes(search.toLowerCase()))
    : filtered;

  return (
    <>
      <Head>
        <title>MallX — كل اللي محتاجه في مول واحد</title>
        <meta name="description" content="تسوق، اطلب طعام، احجز خدمات — كل شيء في مكان واحد"/>
        <meta name="viewport" content="width=device-width, initial-scale=1"/>
      </Head>

      <div className="marketplace" dir="rtl">
        {/* ── Top Nav ──────────────────────────────────────────────────── */}
        <nav className="nav">
          <div className="nav-inner">
            <div className="nav-brand">🏬 MallX</div>
            <div className="nav-search">
              <input
                value={search}
                onChange={e => setSearch(e.target.value)}
                placeholder="ابحث عن منتجات، مطاعم، خدمات..."
                className="search-input"/>
            </div>
            <div className="nav-actions">
              {token
                ? <Link href="/mall-admin" className="btn-primary">لوحة التحكم</Link>
                : <>
                    <Link href="/login" className="btn-ghost">تسجيل الدخول</Link>
                    <Link href="/register" className="btn-primary">ابدأ مجاناً</Link>
                  </>}
            </div>
          </div>
        </nav>

        {/* ── Hero ─────────────────────────────────────────────────────── */}
        <section className="hero">
          <div className="hero-content">
            <h1>كل اللي محتاجه في<br/><span className="gradient-text">مول واحد</span></h1>
            <p>تسوق، اطلب طعام، احجز مواعيد — اكتشف عالم من المحلات</p>
            <div className="hero-btns">
              <a href="#products" className="btn-primary btn-lg">تسوق الآن</a>
              <a href="#stores"   className="btn-outline btn-lg">اكتشف المحلات</a>
            </div>
            <div className="hero-stats">
              <div className="stat"><strong>{stores.length}+</strong> محل</div>
              <div className="stat"><strong>{products.length}+</strong> منتج</div>
              <div className="stat"><strong>⭐ 4.8</strong> تقييم</div>
            </div>
          </div>
          <div className="hero-visual">
            <div className="hero-emoji">🛍️</div>
          </div>
        </section>

        <main className="main">

          {/* ── Flash Sales ───────────────────────────────────────────── */}
          {flash.length > 0 && (
            <section className="section">
              <div className="section-header">
                <h2>⚡ عروض محدودة</h2>
                <span className="badge-red">لوقت محدود</span>
              </div>
              <div className="flash-grid">
                {flash.slice(0, 4).map((f: any) => (
                  <div key={f.id} className="flash-card">
                    <div className="flash-discount">-{Math.round(((f.originalPrice - f.flashPrice) / f.originalPrice) * 100)}%</div>
                    <h3 className="flash-title">{f.titleAr}</h3>
                    <div className="flash-prices">
                      <span className="price-new">{f.flashPrice} ج.م</span>
                      <span className="price-old">{f.originalPrice} ج.م</span>
                    </div>
                    <div className="flash-stock">
                      تبقّى {f.quantityLimit - f.quantitySold} قطعة
                      <div className="progress">
                        <div className="progress-bar" style={{width: `${(f.quantitySold / f.quantityLimit) * 100}%`}}/>
                      </div>
                    </div>
                    <button onClick={() => guardedAction('/checkout')} className="btn-flash">
                      اشترِ الآن
                    </button>
                  </div>
                ))}
              </div>
            </section>
          )}

          {/* ── Categories ──────────────────────────────────────────────── */}
          <section className="section">
            <h2>تصفّح حسب الفئة</h2>
            <div className="cat-scroll">
              {[{id:'all', name:'الكل', icon:'🏬'}, ...categories].map(c => (
                <button key={c.id}
                  onClick={() => setCat(c.id)}
                  className={`cat-pill ${catFilter === c.id ? 'active' : ''}`}>
                  <span className="cat-icon">{c.icon}</span>
                  <span>{c.name}</span>
                </button>
              ))}
            </div>
          </section>

          {/* ── Stores ──────────────────────────────────────────────────── */}
          <section id="stores" className="section">
            <div className="section-header">
              <h2>🏪 أبرز المحلات</h2>
            </div>
            <div className="stores-grid">
              {stores.slice(0, 8).map(s => (
                <Link key={s.id} href={`/store/${s.id}`} className="store-card">
                  <div className="store-icon">{
                    s.storeType === 'Restaurant' ? '🍔' :
                    s.storeType === 'Service'    ? '💇' : '🛍️'
                  }</div>
                  <div className="store-info">
                    <h3>{s.name}</h3>
                    {s.avgRating && (
                      <div className="rating">⭐ {s.avgRating.toFixed(1)}</div>
                    )}
                    <span className="store-type">{
                      s.storeType === 'Restaurant' ? 'مطعم' :
                      s.storeType === 'Service'    ? 'خدمة' : 'متجر'
                    }</span>
                  </div>
                </Link>
              ))}
            </div>
          </section>

          {/* ── Products ──────────────────────────────────────────────── */}
          <section id="products" className="section">
            <div className="section-header">
              <h2>🛒 المنتجات المتاحة</h2>
              <span className="count">{searched.length} منتج</span>
            </div>
            {loading ? (
              <div className="skeleton-grid">
                {Array.from({length: 8}).map((_,i) => (
                  <div key={i} className="skeleton-card"/>))}
              </div>
            ) : searched.length === 0 ? (
              <div className="empty">لا توجد نتائج 🔍</div>
            ) : (
              <div className="products-grid">
                {searched.slice(0, 24).map(p => (
                  <div key={p.id} className="product-card">
                    <div className="product-img">
                      {p.imageUrl
                        ? <img src={p.imageUrl} alt={p.name} loading="lazy"/>
                        : <span className="product-placeholder">📦</span>}
                    </div>
                    <div className="product-info">
                      <h3 className="product-name">{p.name}</h3>
                      <p className="product-store">{p.storeName}</p>
                      {p.avgRating && <div className="rating">⭐ {p.avgRating.toFixed(1)}</div>}
                      <div className="product-footer">
                        <span className="product-price">{p.price} ج.م</span>
                        <button
                          onClick={() => guardedAction('/login')}
                          className="btn-add">
                          أضف للسلة
                        </button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </section>

          {/* ── CTA ─────────────────────────────────────────────────────── */}
          <section className="cta-section">
            <div className="cta-card">
              <h2>هل أنت صاحب محل؟</h2>
              <p>انضم إلى MallX وابدأ البيع لآلاف العملاء اليوم</p>
              <div className="cta-features">
                {['لوحة تحكم متكاملة', 'تحليلات مبيعات', 'نظام دفع آمن', 'دعم فني 24/7'].map(f => (
                  <div key={f} className="cta-feature">✅ {f}</div>
                ))}
              </div>
              <Link href="/register?role=store" className="btn-primary btn-lg">
                ابدأ مجاناً — لا بطاقة مطلوبة
              </Link>
            </div>
          </section>
        </main>

        {/* ── Footer ──────────────────────────────────────────────────── */}
        <footer className="footer">
          <div className="footer-inner">
            <div className="footer-brand">🏬 MallX</div>
            <p>كل اللي محتاجه في مول واحد</p>
            <div className="footer-links">
              <a href="#">الشروط والأحكام</a>
              <a href="#">سياسة الخصوصية</a>
              <a href="/mall-admin">لوحة التحكم</a>
            </div>
            <p className="footer-copy">© 2026 MallX — Excellence86</p>
          </div>
        </footer>
      </div>

      <style jsx global>{`
        *{box-sizing:border-box;margin:0;padding:0}
        body{font-family:'Cairo',Arial,sans-serif;background:#0a0f1a;color:#f1f5f9}
        a{color:inherit;text-decoration:none}

        .marketplace{min-height:100vh}

        /* Nav */
        .nav{background:#0f172a;border-bottom:1px solid #1e293b;position:sticky;top:0;z-index:100}
        .nav-inner{max-width:1200px;margin:auto;display:flex;align-items:center;gap:16px;padding:12px 20px}
        .nav-brand{font-size:20px;font-weight:900;white-space:nowrap}
        .nav-search{flex:1}
        .search-input{width:100%;background:#1e293b;border:1px solid #334155;border-radius:10px;padding:8px 14px;color:#f1f5f9;font-family:inherit;font-size:13px}
        .search-input:focus{outline:none;border-color:#3b82f6}
        .nav-actions{display:flex;gap:8px;white-space:nowrap}

        /* Buttons */
        .btn-primary{background:#3b82f6;color:#fff;padding:8px 16px;border-radius:8px;font-weight:700;font-size:13px;border:none;cursor:pointer}
        .btn-primary.btn-lg{padding:14px 28px;font-size:15px;border-radius:12px}
        .btn-outline{background:transparent;color:#3b82f6;border:1px solid #3b82f6;padding:14px 28px;border-radius:12px;font-weight:700;font-size:15px;cursor:pointer}
        .btn-ghost{background:transparent;color:#94a3b8;padding:8px 14px;border-radius:8px;font-size:13px;border:none;cursor:pointer}
        .btn-add{background:#3b82f6;color:#fff;border:none;border-radius:8px;padding:6px 12px;font-size:11px;font-weight:700;cursor:pointer;white-space:nowrap}
        .btn-flash{width:100%;background:#f59e0b;color:#fff;border:none;border-radius:8px;padding:10px;font-weight:700;font-size:13px;cursor:pointer;margin-top:10px}

        /* Hero */
        .hero{max-width:1200px;margin:0 auto;padding:60px 20px;display:flex;align-items:center;justify-content:space-between;gap:40px}
        .hero-content{flex:1}
        .hero h1{font-size:clamp(28px,4vw,48px);font-weight:900;line-height:1.2;margin-bottom:16px}
        .gradient-text{background:linear-gradient(135deg,#3b82f6,#10b981);-webkit-background-clip:text;-webkit-text-fill-color:transparent}
        .hero p{color:#94a3b8;font-size:16px;margin-bottom:24px}
        .hero-btns{display:flex;gap:12px;flex-wrap:wrap;margin-bottom:32px}
        .hero-stats{display:flex;gap:24px}
        .stat{color:#94a3b8;font-size:13px}.stat strong{color:#f1f5f9;font-weight:800;display:block;font-size:20px}
        .hero-visual{flex-shrink:0}
        .hero-emoji{font-size:120px;line-height:1}

        /* Main */
        .main{max-width:1200px;margin:0 auto;padding:0 20px 60px}
        .section{margin-bottom:48px}
        .section-header{display:flex;align-items:center;justify-content:space-between;margin-bottom:20px}
        .section h2{font-size:20px;font-weight:800;color:#f1f5f9}
        .count{background:#1e293b;border:1px solid #334155;border-radius:20px;padding:3px 12px;font-size:12px;color:#94a3b8}
        .badge-red{background:#ef444420;color:#ef4444;border-radius:6px;padding:3px 10px;font-size:11px;font-weight:700}

        /* Flash */
        .flash-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:16px}
        .flash-card{background:#1e293b;border:1px solid #f59e0b40;border-radius:14px;padding:16px;position:relative}
        .flash-discount{position:absolute;top:12px;left:12px;background:#ef4444;color:#fff;border-radius:6px;padding:2px 8px;font-size:12px;font-weight:800}
        .flash-title{font-size:14px;font-weight:700;margin:20px 0 10px}
        .flash-prices{display:flex;align-items:center;gap:10px;margin-bottom:8px}
        .price-new{font-size:18px;font-weight:900;color:#3b82f6}
        .price-old{font-size:12px;color:#64748b;text-decoration:line-through}
        .flash-stock{font-size:11px;color:#94a3b8;margin-bottom:6px}
        .progress{background:#334155;border-radius:4px;height:4px;margin-top:4px}
        .progress-bar{background:#f59e0b;height:4px;border-radius:4px;transition:width .3s}

        /* Categories */
        .cat-scroll{display:flex;gap:10px;overflow-x:auto;padding-bottom:4px;-webkit-overflow-scrolling:touch}
        .cat-scroll::-webkit-scrollbar{display:none}
        .cat-pill{background:#1e293b;border:1px solid #334155;border-radius:24px;padding:8px 16px;color:#94a3b8;font-size:13px;font-weight:600;cursor:pointer;white-space:nowrap;display:flex;align-items:center;gap:6px;transition:all .2s}
        .cat-pill.active,.cat-pill:hover{background:#3b82f610;border-color:#3b82f6;color:#3b82f6}
        .cat-icon{font-size:14px}

        /* Stores */
        .stores-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(160px,1fr));gap:14px}
        .store-card{background:#1e293b;border:1px solid #334155;border-radius:14px;padding:16px;display:flex;flex-direction:column;align-items:center;text-align:center;gap:10px;transition:border-color .2s;cursor:pointer}
        .store-card:hover{border-color:#3b82f6}
        .store-icon{font-size:36px}
        .store-info h3{font-size:13px;font-weight:700;color:#f1f5f9;margin-bottom:4px}
        .store-type{background:#3b82f610;color:#3b82f6;border-radius:6px;padding:2px 8px;font-size:10px;font-weight:700}
        .rating{font-size:11px;color:#f59e0b;margin-bottom:4px}

        /* Products */
        .products-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:16px}
        .product-card{background:#1e293b;border:1px solid #334155;border-radius:14px;overflow:hidden;transition:transform .2s,border-color .2s}
        .product-card:hover{transform:translateY(-2px);border-color:#3b82f6}
        .product-img{height:130px;background:#0f172a;display:flex;align-items:center;justify-content:center;overflow:hidden}
        .product-img img{width:100%;height:100%;object-fit:cover}
        .product-placeholder{font-size:40px}
        .product-info{padding:12px}
        .product-name{font-size:13px;font-weight:700;color:#f1f5f9;margin-bottom:4px;overflow:hidden;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical}
        .product-store{font-size:11px;color:#64748b;margin-bottom:4px}
        .product-footer{display:flex;align-items:center;justify-content:space-between;margin-top:8px}
        .product-price{font-size:14px;font-weight:900;color:#3b82f6}

        /* Skeletons */
        .skeleton-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:16px}
        .skeleton-card{height:230px;background:#1e293b;border-radius:14px;animation:pulse 1.5s ease-in-out infinite}
        @keyframes pulse{0%,100%{opacity:1}50%{opacity:.5}}

        /* CTA */
        .cta-section{margin-top:60px}
        .cta-card{background:linear-gradient(135deg,#1e3a5f,#0f172a);border:1px solid #3b82f640;border-radius:20px;padding:40px;text-align:center}
        .cta-card h2{font-size:24px;font-weight:900;margin-bottom:10px}
        .cta-card p{color:#94a3b8;margin-bottom:24px}
        .cta-features{display:flex;flex-wrap:wrap;justify-content:center;gap:16px;margin-bottom:28px}
        .cta-feature{font-size:13px;color:#94a3b8}

        /* Footer */
        .footer{background:#0f172a;border-top:1px solid #1e293b;margin-top:60px}
        .footer-inner{max-width:1200px;margin:auto;padding:40px 20px;text-align:center}
        .footer-brand{font-size:24px;font-weight:900;margin-bottom:8px}
        .footer-links{display:flex;justify-content:center;gap:20px;margin:16px 0;flex-wrap:wrap}
        .footer-links a{color:#64748b;font-size:13px}
        .footer-links a:hover{color:#94a3b8}
        .footer-copy{color:#334155;font-size:12px;margin-top:16px}
        .empty{text-align:center;color:#64748b;padding:60px;font-size:16px}

        @media(max-width:640px){
          .hero{flex-direction:column;padding:40px 16px;text-align:center}
          .hero-visual{display:none}
          .hero-btns{justify-content:center}
          .hero-stats{justify-content:center}
          .nav-inner{flex-wrap:wrap}
          .nav-search{order:3;width:100%}
        }
      `}</style>
    </>
  );

  function guardedAction(url: string) {
    if (!token) { window.location.href = '/login?redirect=' + encodeURIComponent(url); return; }
    window.location.href = url;
  }
}
