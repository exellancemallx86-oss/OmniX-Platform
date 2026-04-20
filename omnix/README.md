# 🏢 OmniX Business Platform

> **منصة إدارة الأعمال الموحّدة** — مدمجة من MesterX Pro v7 + MallX SAAS + Ultra Enterprise v6

---

## 🧩 الـ Modules المتاحة

| Module | الوصف | المصدر |
|--------|-------|--------|
| **POS** | نقطة بيع كاملة + مخزون + موردين | Ultra v6 |
| **Restaurant** | مطعم enterprise — Floor Plan + Kitchen + QR | Ultra v6 |
| **Rental** | إيجار أصول — ملابس/معدات/أثاث | Ultra v6 |
| **Mall B2C** | مول تجاري متعدد البائعين | MallX |
| **Loyalty & Wallet** | نقاط ولاء + محفظة رقمية | MallX |
| **Offline Sync** | مزامنة بدون إنترنت | MesterX Pro |
| **AI Assistant** | مساعد ذكي Claude | MallX + Pro |
| **License Engine** | اشتراكات + تفعيل license | Ultra v6 |

---

## 🏗️ Architecture

```
OmniX.Domain          ← Entities + Enums (Business logic)
OmniX.Application     ← Services + Interfaces (Use cases)
OmniX.Infrastructure  ← DbContext + Redis + Migrations
OmniX.API             ← Controllers + Hubs + Middleware
OmniX.Tests           ← Unit + Integration tests
```

---

## 🚀 تشغيل محلي

```bash
# 1. Clone
git clone https://github.com/YOUR_USERNAME/omnix-platform.git
cd omnix-platform

# 2. متطلبات
# - .NET 8 SDK
# - PostgreSQL 16
# - Redis 7
# - Node.js 20

# 3. إعداد البيئة
cp backend/src/OmniX.API/appsettings.json backend/src/OmniX.API/appsettings.Development.json
# عدّل: ConnectionStrings, Jwt:Secret

# 4. تشغيل
cd backend
dotnet run --project src/OmniX.API

# 5. Frontend
cd ../frontend
npm install && npm run dev
```

**Swagger:** http://localhost:5000/swagger

---

## ☁️ Deploy على Render

```bash
git push origin main
# Render → New → Blueprint → اختر الـ repo
# render.yaml يبني كل شيء تلقائياً
```

**المتغيرات المطلوبة من Render Dashboard:**
- `Paymob__ApiKey` + `Paymob__IntegrationId` + `Paymob__HmacSecret`
- `Firebase__ServerKey` + `Firebase__ProjectId`
- `Anthropic__ApiKey`

---

## 🔌 API Endpoints الرئيسية

```
# Auth (B2B Staff)
POST /api/v1/auth/login
POST /api/v1/auth/register
POST /api/v1/auth/refresh
POST /api/v1/auth/totp/enable      ← TOTP 2FA

# Auth (B2C Mall Customer)
POST /api/v1/mall/auth/login
POST /api/v1/mall/auth/register

# POS
GET  /api/v1/pos/products
POST /api/v1/pos/sale
GET  /api/v1/pos/reports/daily

# Restaurant
GET  /api/v1/restaurant/menu
POST /api/v1/restaurant/orders
GET  /api/v1/restaurant/kitchen/tickets
POST /api/v1/restaurant/tables/{id}/session/open

# Rental
GET  /api/v1/rental/assets
POST /api/v1/rental/bookings
POST /api/v1/rental/bookings/{id}/pickup

# Mall B2C
GET  /api/v1/mall/stores
GET  /api/v1/mall/products
POST /api/v1/mall/orders
GET  /api/v1/mall/orders/{id}/track

# Sync (Offline)
POST /api/v1/sync/push
GET  /api/v1/sync/pull

# Licensing
GET  /api/v1/license/status
POST /api/v1/license/activate
```

---

## 🔴 SignalR Hubs

| Hub | URL | الاستخدام |
|-----|-----|-----------|
| Restaurant | `/hubs/restaurant` | Kitchen + Floor Plan + QR |
| Mall Orders | `/hubs/mall-orders` | Order Tracking + Driver GPS |

---

## ✅ Health Checks

```
GET /health        ← كل الـ checks
GET /health/live   ← API حي؟
GET /health/ready  ← DB + Redis جاهزان؟
```

---

## 🧪 Tests

```bash
cd backend
dotnet test tests/OmniX.Tests/OmniX.Tests.csproj -v normal
```

---

## 📋 متغيرات البيئة الكاملة

| المتغير | الوصف |
|---------|-------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `ConnectionStrings__Redis` | Redis connection string |
| `Jwt__Secret` | ≥ 64 حرف |
| `Jwt__Issuer` | OmniX |
| `Jwt__Audience` | OmniXClient |
| `Paymob__ApiKey` | من Paymob Dashboard |
| `Paymob__IntegrationId` | من Paymob Dashboard |
| `Paymob__HmacSecret` | من Paymob Dashboard |
| `Firebase__ServerKey` | من Firebase Console |
| `Anthropic__ApiKey` | من Anthropic Console |
| `AllowedOrigins__0` | URL الـ Frontend |

---

*OmniX Platform — مدمج من 3 مشاريع، 298+ ملف، بدون تشوهات*
