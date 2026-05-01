// ═══════════════════════════════════════════════════════════════════════════
//  OmniX Platform — Initial EF Core Migration
//  يُنشئ كل الجداول: Auth + Core + POS + Restaurant + Rental + Mall + Licensing
//  تاريخ: 2025-04-30
//  الاستخدام: يُطبَّق تلقائياً عند startup عبر MigrateAsync()
// ═══════════════════════════════════════════════════════════════════════════
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniX.Infrastructure.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        // ── Enable UUID extension ──────────────────────────────────────────
        m.Sql("CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\";");
        m.Sql("CREATE EXTENSION IF NOT EXISTS \"pg_trgm\";");  // للبحث النصي

        // ══════════════════════════════════════════════════════════════════
        //  1. TENANTS (المستأجرون — قاعدة Multi-tenancy)
        // ══════════════════════════════════════════════════════════════════
        m.CreateTable("tenants", t => new
        {
            id               = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            name             = t.Column<string>(maxLength: 200, nullable: false),
            name_en          = t.Column<string>(maxLength: 200, nullable: true),
            slug             = t.Column<string>(maxLength: 100, nullable: false),
            business_type    = t.Column<short>(nullable: false, defaultValue: (short)0),
            status           = t.Column<short>(nullable: false, defaultValue: (short)0),
            modules          = t.Column<int>(nullable: false, defaultValue: 3),      // Inventory + POS default
            logo_url         = t.Column<string>(maxLength: 500, nullable: true),
            primary_color    = t.Column<string>(maxLength: 7, nullable: true),
            currency         = t.Column<string>(maxLength: 3, nullable: false, defaultValue: "EGP"),
            country_code     = t.Column<string>(maxLength: 2, nullable: false, defaultValue: "EG"),
            timezone         = t.Column<string>(maxLength: 50, nullable: false, defaultValue: "Africa/Cairo"),
            language         = t.Column<string>(maxLength: 5, nullable: false, defaultValue: "ar"),
            tax_number       = t.Column<string>(maxLength: 50, nullable: true),
            is_active        = t.Column<bool>(nullable: false, defaultValue: true),
            trial_ends_at    = t.Column<DateTime>(nullable: true),
            created_at       = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at       = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t => t.PrimaryKey("pk_tenants", x => x.id));

        m.CreateIndex("ix_tenants_slug",      "tenants", "slug",      unique: true);
        m.CreateIndex("ix_tenants_is_active", "tenants", "is_active");

        // ══════════════════════════════════════════════════════════════════
        //  2. BRANCHES (الفروع)
        // ══════════════════════════════════════════════════════════════════
        m.CreateTable("branches", t => new
        {
            id           = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id    = t.Column<Guid>(nullable: false),
            name         = t.Column<string>(maxLength: 200, nullable: false),
            name_en      = t.Column<string>(maxLength: 200, nullable: true),
            address      = t.Column<string>(maxLength: 500, nullable: true),
            city         = t.Column<string>(maxLength: 100, nullable: true),
            phone        = t.Column<string>(maxLength: 20, nullable: true),
            whatsapp     = t.Column<string>(maxLength: 20, nullable: true),
            lat          = t.Column<decimal>(type: "decimal(10,7)", nullable: true),
            lng          = t.Column<decimal>(type: "decimal(10,7)", nullable: true),
            is_main      = t.Column<bool>(nullable: false, defaultValue: false),
            is_active    = t.Column<bool>(nullable: false, defaultValue: true),
            sort_order   = t.Column<int>(nullable: false, defaultValue: 0),
            opening_hours = t.Column<string>(maxLength: 500, nullable: true),
            created_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_branches", x => x.id);
            t.ForeignKey("fk_branches_tenants", x => x.tenant_id, "tenants", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateIndex("ix_branches_tenant", "branches", "tenant_id");

        // ══════════════════════════════════════════════════════════════════
        //  3. AUTH — Users, Roles, Permissions, Tokens
        // ══════════════════════════════════════════════════════════════════
        m.CreateTable("application_roles", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            name        = t.Column<string>(maxLength: 100, nullable: false),
            name_ar     = t.Column<string>(maxLength: 100, nullable: false),
            description = t.Column<string>(maxLength: 500, nullable: true),
            is_system   = t.Column<bool>(nullable: false, defaultValue: false),
            is_active   = t.Column<bool>(nullable: false, defaultValue: true),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_application_roles", x => x.id);
            t.ForeignKey("fk_roles_tenants", x => x.tenant_id, "tenants", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("application_users", t => new
        {
            id                    = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id             = t.Column<Guid>(nullable: false),
            branch_id             = t.Column<Guid>(nullable: true),
            role_id               = t.Column<Guid>(nullable: true),
            username              = t.Column<string>(maxLength: 100, nullable: false),
            email                 = t.Column<string>(maxLength: 200, nullable: false),
            password_hash         = t.Column<string>(maxLength: 500, nullable: false),
            first_name            = t.Column<string>(maxLength: 100, nullable: false),
            last_name             = t.Column<string>(maxLength: 100, nullable: false),
            phone                 = t.Column<string>(maxLength: 20, nullable: true),
            avatar_url            = t.Column<string>(maxLength: 500, nullable: true),
            role                  = t.Column<short>(nullable: false, defaultValue: (short)5),
            is_active             = t.Column<bool>(nullable: false, defaultValue: true),
            is_email_verified     = t.Column<bool>(nullable: false, defaultValue: false),
            is_phone_verified     = t.Column<bool>(nullable: false, defaultValue: false),
            email_verified_at     = t.Column<DateTime>(nullable: true),
            last_login_at         = t.Column<DateTime>(nullable: true),
            last_login_ip         = t.Column<string>(maxLength: 45, nullable: true),
            last_login_device     = t.Column<string>(maxLength: 200, nullable: true),
            failed_login_attempts = t.Column<int>(nullable: false, defaultValue: 0),
            lockout_end           = t.Column<DateTime>(nullable: true),
            two_factor_enabled    = t.Column<bool>(nullable: false, defaultValue: false),
            totp_secret           = t.Column<string>(maxLength: 500, nullable: true),
            backup_codes          = t.Column<string>(nullable: true),
            password_changed_at   = t.Column<DateTime>(nullable: true),
            must_change_password  = t.Column<bool>(nullable: false, defaultValue: false),
            language              = t.Column<string>(maxLength: 5, nullable: false, defaultValue: "ar"),
            theme                 = t.Column<string>(maxLength: 20, nullable: false, defaultValue: "dark"),
            timezone              = t.Column<string>(maxLength: 50, nullable: false, defaultValue: "Africa/Cairo"),
            created_at            = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at            = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_application_users", x => x.id);
            t.ForeignKey("fk_users_tenants",  x => x.tenant_id, "tenants",             "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_users_branches", x => x.branch_id, "branches",            "id", onDelete: ReferentialAction.SetNull);
            t.ForeignKey("fk_users_roles",    x => x.role_id,   "application_roles",   "id", onDelete: ReferentialAction.SetNull);
        });

        m.CreateIndex("ix_users_tenant",            "application_users", "tenant_id");
        m.CreateIndex("ix_users_email_tenant",      "application_users", new[] {"email", "tenant_id"}, unique: true);
        m.CreateIndex("ix_users_username_tenant",   "application_users", new[] {"username", "tenant_id"}, unique: true);

        m.CreateTable("application_permissions", t => new
        {
            id         = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id  = t.Column<Guid>(nullable: false),
            role_id    = t.Column<Guid>(nullable: false),
            resource   = t.Column<string>(maxLength: 100, nullable: false),
            action     = t.Column<string>(maxLength: 50, nullable: false),
            can_create = t.Column<bool>(nullable: false, defaultValue: false),
            can_read   = t.Column<bool>(nullable: false, defaultValue: true),
            can_update = t.Column<bool>(nullable: false, defaultValue: false),
            can_delete = t.Column<bool>(nullable: false, defaultValue: false),
            created_at = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_permissions", x => x.id);
            t.ForeignKey("fk_perms_roles", x => x.role_id, "application_roles", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("refresh_tokens", t => new
        {
            id              = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id       = t.Column<Guid>(nullable: false),
            user_id         = t.Column<Guid>(nullable: false),
            token_hash      = t.Column<string>(maxLength: 500, nullable: false),
            salt            = t.Column<string>(maxLength: 100, nullable: false),
            device_info     = t.Column<string>(maxLength: 200, nullable: true),
            ip_address      = t.Column<string>(maxLength: 45, nullable: true),
            is_revoked      = t.Column<bool>(nullable: false, defaultValue: false),
            expires_at      = t.Column<DateTime>(nullable: false),
            revoked_at      = t.Column<DateTime>(nullable: true),
            replaced_by_token = t.Column<string>(maxLength: 100, nullable: true),
            created_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_refresh_tokens", x => x.id);
            t.ForeignKey("fk_tokens_users", x => x.user_id, "application_users", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("feature_flags", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            key         = t.Column<string>(maxLength: 100, nullable: false),
            is_enabled  = t.Column<bool>(nullable: false, defaultValue: false),
            value       = t.Column<string>(maxLength: 500, nullable: true),
            description = t.Column<string>(maxLength: 500, nullable: true),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t => t.PrimaryKey("pk_feature_flags", x => x.id));

        m.CreateIndex("ix_flags_tenant_key", "feature_flags", new[] {"tenant_id", "key"}, unique: true);

        m.CreateTable("audit_logs", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            user_id     = t.Column<Guid>(nullable: true),
            action      = t.Column<string>(maxLength: 100, nullable: false),
            entity      = t.Column<string>(maxLength: 100, nullable: false),
            entity_id   = t.Column<string>(maxLength: 100, nullable: true),
            old_values  = t.Column<string>(nullable: true),
            new_values  = t.Column<string>(nullable: true),
            ip_address  = t.Column<string>(maxLength: 45, nullable: true),
            user_agent  = t.Column<string>(maxLength: 500, nullable: true),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t => t.PrimaryKey("pk_audit_logs", x => x.id));

        m.CreateIndex("ix_audit_tenant",   "audit_logs", "tenant_id");
        m.CreateIndex("ix_audit_created",  "audit_logs", "created_at");

        // ══════════════════════════════════════════════════════════════════
        //  4. CORE — Products, Categories, Customers, Suppliers
        // ══════════════════════════════════════════════════════════════════
        m.CreateTable("categories", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            name        = t.Column<string>(maxLength: 200, nullable: false),
            name_en     = t.Column<string>(maxLength: 200, nullable: true),
            parent_id   = t.Column<Guid>(nullable: true),
            icon        = t.Column<string>(maxLength: 100, nullable: true),
            image_url   = t.Column<string>(maxLength: 500, nullable: true),
            color       = t.Column<string>(maxLength: 7, nullable: true),
            sort_order  = t.Column<int>(nullable: false, defaultValue: 0),
            is_active   = t.Column<bool>(nullable: false, defaultValue: true),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_categories", x => x.id);
            t.ForeignKey("fk_categories_tenant", x => x.tenant_id, "tenants", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("products", t => new
        {
            id                = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id         = t.Column<Guid>(nullable: false),
            branch_id         = t.Column<Guid>(nullable: true),
            category_id       = t.Column<Guid>(nullable: true),
            name              = t.Column<string>(maxLength: 200, nullable: false),
            name_en           = t.Column<string>(maxLength: 200, nullable: true),
            description       = t.Column<string>(maxLength: 2000, nullable: true),
            barcode           = t.Column<string>(maxLength: 100, nullable: true),
            sku               = t.Column<string>(maxLength: 100, nullable: true),
            product_type      = t.Column<short>(nullable: false, defaultValue: (short)0),
            unit              = t.Column<short>(nullable: false, defaultValue: (short)0),
            purchase_price    = t.Column<decimal>(type: "decimal(12,4)", nullable: false, defaultValue: 0m),
            sale_price        = t.Column<decimal>(type: "decimal(12,4)", nullable: false),
            min_sale_price    = t.Column<decimal>(type: "decimal(12,4)", nullable: false, defaultValue: 0m),
            tax_rate          = t.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 0m),
            image_url         = t.Column<string>(maxLength: 500, nullable: true),
            tags              = t.Column<string>(maxLength: 500, nullable: true),
            low_stock_alert   = t.Column<int>(nullable: false, defaultValue: 5),
            is_active         = t.Column<bool>(nullable: false, defaultValue: true),
            is_trackable      = t.Column<bool>(nullable: false, defaultValue: true),
            created_at        = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at        = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_products", x => x.id);
            t.ForeignKey("fk_products_tenants",    x => x.tenant_id,   "tenants",    "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_products_categories", x => x.category_id, "categories", "id", onDelete: ReferentialAction.SetNull);
        });

        m.CreateIndex("ix_products_tenant",  "products", "tenant_id");
        m.CreateIndex("ix_products_barcode", "products", new[] {"barcode", "tenant_id"});
        m.CreateIndex("ix_products_name_trgm", "products", "name");

        m.CreateTable("customers", t => new
        {
            id              = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id       = t.Column<Guid>(nullable: false),
            name            = t.Column<string>(maxLength: 200, nullable: false),
            name_en         = t.Column<string>(maxLength: 200, nullable: true),
            phone           = t.Column<string>(maxLength: 20, nullable: true),
            email           = t.Column<string>(maxLength: 200, nullable: true),
            address         = t.Column<string>(maxLength: 500, nullable: true),
            tax_number      = t.Column<string>(maxLength: 50, nullable: true),
            credit_limit    = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            current_balance = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            loyalty_points  = t.Column<int>(nullable: false, defaultValue: 0),
            notes           = t.Column<string>(maxLength: 1000, nullable: true),
            is_active       = t.Column<bool>(nullable: false, defaultValue: true),
            created_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_customers", x => x.id);
            t.ForeignKey("fk_customers_tenants", x => x.tenant_id, "tenants", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateIndex("ix_customers_tenant", "customers", "tenant_id");
        m.CreateIndex("ix_customers_phone",  "customers", new[] {"phone", "tenant_id"});

        m.CreateTable("suppliers", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            name        = t.Column<string>(maxLength: 200, nullable: false),
            phone       = t.Column<string>(maxLength: 20, nullable: true),
            email       = t.Column<string>(maxLength: 200, nullable: true),
            address     = t.Column<string>(maxLength: 500, nullable: true),
            tax_number  = t.Column<string>(maxLength: 50, nullable: true),
            balance     = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            notes       = t.Column<string>(maxLength: 1000, nullable: true),
            is_active   = t.Column<bool>(nullable: false, defaultValue: true),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_suppliers", x => x.id);
            t.ForeignKey("fk_suppliers_tenants", x => x.tenant_id, "tenants", "id", onDelete: ReferentialAction.Cascade);
        });

        // ══════════════════════════════════════════════════════════════════
        //  5. POS / INVENTORY
        // ══════════════════════════════════════════════════════════════════
        m.CreateTable("stock_items", t => new
        {
            id           = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id    = t.Column<Guid>(nullable: false),
            branch_id    = t.Column<Guid>(nullable: false),
            product_id   = t.Column<Guid>(nullable: false),
            quantity     = t.Column<decimal>(type: "decimal(12,3)", nullable: false, defaultValue: 0m),
            reserved_qty = t.Column<decimal>(type: "decimal(12,3)", nullable: false, defaultValue: 0m),
            created_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_stock_items", x => x.id);
            t.ForeignKey("fk_stock_tenants",   x => x.tenant_id,  "tenants",  "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_stock_branches",  x => x.branch_id,  "branches", "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_stock_products",  x => x.product_id, "products", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateIndex("ix_stock_branch_product", "stock_items", new[] {"branch_id", "product_id"}, unique: true);
        m.CreateIndex("ix_stock_low",            "stock_items", "quantity");

        m.CreateTable("stock_movements", t => new
        {
            id              = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id       = t.Column<Guid>(nullable: false),
            branch_id       = t.Column<Guid>(nullable: false),
            product_id      = t.Column<Guid>(nullable: false),
            user_id         = t.Column<Guid>(nullable: true),
            movement_type   = t.Column<short>(nullable: false),
            quantity        = t.Column<decimal>(type: "decimal(12,3)", nullable: false),
            unit_cost       = t.Column<decimal>(type: "decimal(12,4)", nullable: false, defaultValue: 0m),
            balance_before  = t.Column<decimal>(type: "decimal(12,3)", nullable: false),
            balance_after   = t.Column<decimal>(type: "decimal(12,3)", nullable: false),
            reference       = t.Column<string>(maxLength: 100, nullable: true),
            notes           = t.Column<string>(maxLength: 500, nullable: true),
            created_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_stock_movements", x => x.id);
            t.ForeignKey("fk_smov_tenants",  x => x.tenant_id,  "tenants",  "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_smov_branches", x => x.branch_id,  "branches", "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_smov_products", x => x.product_id, "products", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateIndex("ix_smov_tenant_created", "stock_movements", new[] {"tenant_id", "created_at"});

        m.CreateTable("sale_orders", t => new
        {
            id              = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id       = t.Column<Guid>(nullable: false),
            branch_id       = t.Column<Guid>(nullable: false),
            customer_id     = t.Column<Guid>(nullable: true),
            user_id         = t.Column<Guid>(nullable: false),
            order_number    = t.Column<string>(maxLength: 50, nullable: false),
            status          = t.Column<short>(nullable: false, defaultValue: (short)0),
            subtotal        = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            discount_amount = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            tax_amount      = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            total           = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            paid_amount     = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            change_amount   = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            payment_method  = t.Column<short>(nullable: false, defaultValue: (short)0),
            notes           = t.Column<string>(maxLength: 500, nullable: true),
            is_refunded     = t.Column<bool>(nullable: false, defaultValue: false),
            refund_reason   = t.Column<string>(maxLength: 500, nullable: true),
            refunded_at     = t.Column<DateTime>(nullable: true),
            created_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_sale_orders", x => x.id);
            t.ForeignKey("fk_sale_tenants",   x => x.tenant_id,   "tenants",             "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_sale_branches",  x => x.branch_id,   "branches",            "id");
            t.ForeignKey("fk_sale_customers", x => x.customer_id, "customers",           "id", onDelete: ReferentialAction.SetNull);
            t.ForeignKey("fk_sale_users",     x => x.user_id,     "application_users",   "id");
        });

        m.CreateIndex("ix_sale_tenant_created",  "sale_orders", new[] {"tenant_id", "created_at"});
        m.CreateIndex("ix_sale_branch_created",  "sale_orders", new[] {"branch_id", "created_at"});
        m.CreateIndex("ix_sale_order_number",    "sale_orders", new[] {"order_number", "tenant_id"}, unique: true);

        m.CreateTable("sale_order_items", t => new
        {
            id           = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id    = t.Column<Guid>(nullable: false),
            order_id     = t.Column<Guid>(nullable: false),
            product_id   = t.Column<Guid>(nullable: false),
            product_name = t.Column<string>(maxLength: 200, nullable: false),
            quantity     = t.Column<decimal>(type: "decimal(12,3)", nullable: false),
            unit_price   = t.Column<decimal>(type: "decimal(12,4)", nullable: false),
            discount     = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            tax_amount   = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            total        = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            notes        = t.Column<string>(maxLength: 200, nullable: true),
            created_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_sale_order_items", x => x.id);
            t.ForeignKey("fk_soi_orders",   x => x.order_id,   "sale_orders", "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_soi_products",  x => x.product_id, "products",    "id");
        });

        m.CreateTable("purchase_orders", t => new
        {
            id              = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id       = t.Column<Guid>(nullable: false),
            branch_id       = t.Column<Guid>(nullable: false),
            supplier_id     = t.Column<Guid>(nullable: true),
            user_id         = t.Column<Guid>(nullable: false),
            order_number    = t.Column<string>(maxLength: 50, nullable: false),
            status          = t.Column<short>(nullable: false, defaultValue: (short)0),
            subtotal        = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            tax_amount      = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            total           = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            notes           = t.Column<string>(maxLength: 1000, nullable: true),
            received_at     = t.Column<DateTime>(nullable: true),
            created_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_purchase_orders", x => x.id);
            t.ForeignKey("fk_po_tenants",   x => x.tenant_id,  "tenants",   "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_po_branches",  x => x.branch_id,  "branches",  "id");
            t.ForeignKey("fk_po_suppliers", x => x.supplier_id,"suppliers", "id", onDelete: ReferentialAction.SetNull);
        });

        m.CreateTable("purchase_order_items", t => new
        {
            id           = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id    = t.Column<Guid>(nullable: false),
            order_id     = t.Column<Guid>(nullable: false),
            product_id   = t.Column<Guid>(nullable: false),
            product_name = t.Column<string>(maxLength: 200, nullable: false),
            quantity     = t.Column<decimal>(type: "decimal(12,3)", nullable: false),
            unit_cost    = t.Column<decimal>(type: "decimal(12,4)", nullable: false),
            total        = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            received_qty = t.Column<decimal>(type: "decimal(12,3)", nullable: false, defaultValue: 0m),
            created_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_purchase_order_items", x => x.id);
            t.ForeignKey("fk_poi_orders",  x => x.order_id,  "purchase_orders", "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_poi_products",x => x.product_id,"products",         "id");
        });

        m.CreateTable("payments", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            order_id    = t.Column<Guid>(nullable: true),
            amount      = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            method      = t.Column<short>(nullable: false, defaultValue: (short)0),
            status      = t.Column<short>(nullable: false, defaultValue: (short)0),
            gateway_ref = t.Column<string>(maxLength: 200, nullable: true),
            notes       = t.Column<string>(maxLength: 500, nullable: true),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t => t.PrimaryKey("pk_payments", x => x.id));

        m.CreateTable("expenses", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            branch_id   = t.Column<Guid>(nullable: true),
            user_id     = t.Column<Guid>(nullable: true),
            category    = t.Column<string>(maxLength: 100, nullable: false),
            description = t.Column<string>(maxLength: 500, nullable: false),
            amount      = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            expense_date = t.Column<DateOnly>(nullable: false),
            receipt_url = t.Column<string>(maxLength: 500, nullable: true),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_expenses", x => x.id);
            t.ForeignKey("fk_expenses_tenants", x => x.tenant_id, "tenants", "id", onDelete: ReferentialAction.Cascade);
        });

        // ══════════════════════════════════════════════════════════════════
        //  6. RESTAURANT MODULE
        // ══════════════════════════════════════════════════════════════════
        m.CreateTable("restaurant_categories", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            name_ar     = t.Column<string>(maxLength: 100, nullable: false),
            name_en     = t.Column<string>(maxLength: 100, nullable: true),
            image_url   = t.Column<string>(maxLength: 500, nullable: true),
            color       = t.Column<string>(maxLength: 7, nullable: true),
            sort_order  = t.Column<int>(nullable: false, defaultValue: 0),
            is_active   = t.Column<bool>(nullable: false, defaultValue: true),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_restaurant_categories", x => x.id);
            t.ForeignKey("fk_rc_tenants", x => x.tenant_id, "tenants", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateIndex("ix_rc_tenant_sort", "restaurant_categories", new[] {"tenant_id", "sort_order"});

        m.CreateTable("restaurant_menu_items", t => new
        {
            id                      = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id               = t.Column<Guid>(nullable: false),
            category_id             = t.Column<Guid>(nullable: false),
            default_kitchen_station_id = t.Column<Guid>(nullable: true),
            name_ar                 = t.Column<string>(maxLength: 200, nullable: false),
            name_en                 = t.Column<string>(maxLength: 200, nullable: true),
            description_ar          = t.Column<string>(maxLength: 1000, nullable: true),
            image_url               = t.Column<string>(maxLength: 500, nullable: true),
            base_price              = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            prep_time_minutes       = t.Column<int>(nullable: false, defaultValue: 10),
            availability            = t.Column<int>(nullable: false, defaultValue: 0),  // Available
            is_featured             = t.Column<bool>(nullable: false, defaultValue: false),
            is_active               = t.Column<bool>(nullable: false, defaultValue: true),
            sort_order              = t.Column<int>(nullable: false, defaultValue: 0),
            calories                = t.Column<int>(nullable: true),
            allergens               = t.Column<string>(maxLength: 500, nullable: true),
            created_at              = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at              = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_menu_items", x => x.id);
            t.ForeignKey("fk_mi_tenants",     x => x.tenant_id,   "tenants",               "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_mi_categories",  x => x.category_id, "restaurant_categories", "id");
        });

        m.CreateIndex("ix_mi_tenant_cat",  "restaurant_menu_items", new[] {"tenant_id", "category_id"});
        m.CreateIndex("ix_mi_featured",    "restaurant_menu_items", new[] {"tenant_id", "is_featured"});

        m.CreateTable("menu_item_variants", t => new
        {
            id           = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id    = t.Column<Guid>(nullable: false),
            menu_item_id = t.Column<Guid>(nullable: false),
            name_ar      = t.Column<string>(maxLength: 100, nullable: false),
            name_en      = t.Column<string>(maxLength: 100, nullable: true),
            price_delta  = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            is_default   = t.Column<bool>(nullable: false, defaultValue: false),
            is_active    = t.Column<bool>(nullable: false, defaultValue: true),
            created_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_menu_variants", x => x.id);
            t.ForeignKey("fk_mv_items", x => x.menu_item_id, "restaurant_menu_items", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("menu_modifier_groups", t => new
        {
            id           = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id    = t.Column<Guid>(nullable: false),
            menu_item_id = t.Column<Guid>(nullable: false),
            name_ar      = t.Column<string>(maxLength: 100, nullable: false),
            name_en      = t.Column<string>(maxLength: 100, nullable: true),
            is_required  = t.Column<bool>(nullable: false, defaultValue: false),
            min_select   = t.Column<int>(nullable: false, defaultValue: 0),
            max_select   = t.Column<int>(nullable: false, defaultValue: 1),
            is_active    = t.Column<bool>(nullable: false, defaultValue: true),
            created_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_modifier_groups", x => x.id);
            t.ForeignKey("fk_mg_items", x => x.menu_item_id, "restaurant_menu_items", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("menu_modifiers", t => new
        {
            id         = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id  = t.Column<Guid>(nullable: false),
            group_id   = t.Column<Guid>(nullable: false),
            name_ar    = t.Column<string>(maxLength: 100, nullable: false),
            name_en    = t.Column<string>(maxLength: 100, nullable: true),
            price      = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            is_default = t.Column<bool>(nullable: false, defaultValue: false),
            is_active  = t.Column<bool>(nullable: false, defaultValue: true),
            created_at = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_modifiers", x => x.id);
            t.ForeignKey("fk_mod_groups", x => x.group_id, "menu_modifier_groups", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("branch_menu_overrides", t => new
        {
            id              = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id       = t.Column<Guid>(nullable: false),
            branch_id       = t.Column<Guid>(nullable: false),
            menu_item_id    = t.Column<Guid>(nullable: false),
            override_price  = t.Column<decimal>(type: "decimal(12,2)", nullable: true),
            is_hidden       = t.Column<bool>(nullable: false, defaultValue: false),
            created_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_branch_overrides", x => x.id);
            t.ForeignKey("fk_bo_branches", x => x.branch_id,   "branches",              "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_bo_items",    x => x.menu_item_id,"restaurant_menu_items", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("branch_item_availability", t => new
        {
            id           = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id    = t.Column<Guid>(nullable: false),
            branch_id    = t.Column<Guid>(nullable: false),
            menu_item_id = t.Column<Guid>(nullable: false),
            availability = t.Column<int>(nullable: false, defaultValue: 0),
            created_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_item_availability", x => x.id);
            t.ForeignKey("fk_ia_branches", x => x.branch_id,   "branches",              "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_ia_items",    x => x.menu_item_id,"restaurant_menu_items", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateIndex("ix_ia_branch_item", "branch_item_availability", new[] {"branch_id", "menu_item_id"}, unique: true);

        // ── Floor Plans & Tables ──────────────────────────────────────────
        m.CreateTable("floor_plans", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            branch_id   = t.Column<Guid>(nullable: false),
            name        = t.Column<string>(maxLength: 100, nullable: false),
            name_en     = t.Column<string>(maxLength: 100, nullable: true),
            floor_number = t.Column<int>(nullable: false, defaultValue: 1),
            width       = t.Column<int>(nullable: false, defaultValue: 800),
            height      = t.Column<int>(nullable: false, defaultValue: 600),
            bg_color    = t.Column<string>(maxLength: 7, nullable: true),
            is_active   = t.Column<bool>(nullable: false, defaultValue: true),
            sort_order  = t.Column<int>(nullable: false, defaultValue: 0),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_floor_plans", x => x.id);
            t.ForeignKey("fk_fp_branches", x => x.branch_id, "branches", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("restaurant_tables", t => new
        {
            id           = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id    = t.Column<Guid>(nullable: false),
            branch_id    = t.Column<Guid>(nullable: false),
            floor_plan_id = t.Column<Guid>(nullable: false),
            number       = t.Column<string>(maxLength: 20, nullable: false),
            name         = t.Column<string>(maxLength: 50, nullable: true),
            capacity     = t.Column<int>(nullable: false, defaultValue: 4),
            shape        = t.Column<int>(nullable: false, defaultValue: 0),  // Square
            status       = t.Column<int>(nullable: false, defaultValue: 0),  // Available
            pos_x        = t.Column<int>(nullable: false, defaultValue: 0),
            pos_y        = t.Column<int>(nullable: false, defaultValue: 0),
            width        = t.Column<int>(nullable: false, defaultValue: 80),
            height       = t.Column<int>(nullable: false, defaultValue: 80),
            qr_code      = t.Column<string>(maxLength: 500, nullable: true),
            is_active    = t.Column<bool>(nullable: false, defaultValue: true),
            created_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_restaurant_tables", x => x.id);
            t.ForeignKey("fk_rt_branches",    x => x.branch_id,    "branches",    "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_rt_floor_plans", x => x.floor_plan_id,"floor_plans", "id");
        });

        m.CreateIndex("ix_rt_branch_num", "restaurant_tables", new[] {"branch_id", "number"}, unique: true);

        m.CreateTable("table_sessions", t => new
        {
            id           = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id    = t.Column<Guid>(nullable: false),
            table_id     = t.Column<Guid>(nullable: false),
            waiter_id    = t.Column<Guid>(nullable: true),
            guest_count  = t.Column<int>(nullable: false, defaultValue: 1),
            status       = t.Column<int>(nullable: false, defaultValue: 0),  // Active
            opened_at    = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            bill_sent_at = t.Column<DateTime>(nullable: true),
            closed_at    = t.Column<DateTime>(nullable: true),
            notes        = t.Column<string>(maxLength: 500, nullable: true),
            created_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_table_sessions", x => x.id);
            t.ForeignKey("fk_ts_tables",  x => x.table_id,  "restaurant_tables", "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_ts_waiters", x => x.waiter_id, "application_users", "id", onDelete: ReferentialAction.SetNull);
        });

        m.CreateIndex("ix_ts_table_status", "table_sessions", new[] {"table_id", "status"});

        m.CreateTable("session_payments", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            session_id  = t.Column<Guid>(nullable: false),
            amount      = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            method      = t.Column<short>(nullable: false, defaultValue: (short)0),
            reference   = t.Column<string>(maxLength: 100, nullable: true),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_session_payments", x => x.id);
            t.ForeignKey("fk_sp_sessions", x => x.session_id, "table_sessions", "id", onDelete: ReferentialAction.Cascade);
        });

        // ── Kitchen ───────────────────────────────────────────────────────
        m.CreateTable("kitchen_stations", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            branch_id   = t.Column<Guid>(nullable: false),
            name        = t.Column<string>(maxLength: 100, nullable: false),
            name_en     = t.Column<string>(maxLength: 100, nullable: true),
            color       = t.Column<string>(maxLength: 7, nullable: true),
            printer_ip  = t.Column<string>(maxLength: 50, nullable: true),
            is_active   = t.Column<bool>(nullable: false, defaultValue: true),
            sort_order  = t.Column<int>(nullable: false, defaultValue: 0),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_kitchen_stations", x => x.id);
            t.ForeignKey("fk_ks_branches", x => x.branch_id, "branches", "id", onDelete: ReferentialAction.Cascade);
        });

        // ── Dine-in Orders ────────────────────────────────────────────────
        m.CreateTable("dine_in_orders", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            session_id  = t.Column<Guid>(nullable: false),
            table_id    = t.Column<Guid>(nullable: false),
            waiter_id   = t.Column<Guid>(nullable: true),
            order_number = t.Column<string>(maxLength: 50, nullable: false),
            status      = t.Column<int>(nullable: false, defaultValue: 2),  // Draft
            source      = t.Column<int>(nullable: false, defaultValue: 0),  // Waiter
            subtotal    = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            tax_amount  = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            discount    = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            total       = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            notes       = t.Column<string>(maxLength: 500, nullable: true),
            customer_name = t.Column<string>(maxLength: 100, nullable: true),
            sent_to_kitchen_at = t.Column<DateTime>(nullable: true),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_dine_in_orders", x => x.id);
            t.ForeignKey("fk_dio_sessions", x => x.session_id, "table_sessions",    "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_dio_tables",   x => x.table_id,   "restaurant_tables", "id");
            t.ForeignKey("fk_dio_waiters",  x => x.waiter_id,  "application_users", "id", onDelete: ReferentialAction.SetNull);
        });

        m.CreateIndex("ix_dio_session",  "dine_in_orders", "session_id");
        m.CreateIndex("ix_dio_status",   "dine_in_orders", new[] {"tenant_id", "status"});
        m.CreateIndex("ix_dio_number",   "dine_in_orders", new[] {"order_number", "tenant_id"}, unique: true);

        m.CreateTable("dine_in_order_items", t => new
        {
            id           = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id    = t.Column<Guid>(nullable: false),
            order_id     = t.Column<Guid>(nullable: false),
            menu_item_id = t.Column<Guid>(nullable: false),
            variant_id   = t.Column<Guid>(nullable: true),
            item_name    = t.Column<string>(maxLength: 200, nullable: false),
            quantity     = t.Column<int>(nullable: false, defaultValue: 1),
            unit_price   = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            total        = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            notes        = t.Column<string>(maxLength: 300, nullable: true),
            status       = t.Column<int>(nullable: false, defaultValue: 0),  // Pending
            created_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_dine_in_order_items", x => x.id);
            t.ForeignKey("fk_dioi_orders", x => x.order_id,   "dine_in_orders",        "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_dioi_items",  x => x.menu_item_id,"restaurant_menu_items", "id");
        });

        m.CreateTable("order_item_modifiers", t => new
        {
            id           = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id    = t.Column<Guid>(nullable: false),
            order_item_id = t.Column<Guid>(nullable: false),
            modifier_id  = t.Column<Guid>(nullable: false),
            name         = t.Column<string>(maxLength: 100, nullable: false),
            price        = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            created_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_order_item_modifiers", x => x.id);
            t.ForeignKey("fk_oim_items", x => x.order_item_id, "dine_in_order_items", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("kitchen_tickets", t => new
        {
            id           = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id    = t.Column<Guid>(nullable: false),
            station_id   = t.Column<Guid>(nullable: false),
            order_id     = t.Column<Guid>(nullable: false),
            ticket_number = t.Column<string>(maxLength: 20, nullable: false),
            status       = t.Column<int>(nullable: false, defaultValue: 0),  // New
            priority     = t.Column<int>(nullable: false, defaultValue: 0),
            notes        = t.Column<string>(maxLength: 300, nullable: true),
            seen_at      = t.Column<DateTime>(nullable: true),
            started_at   = t.Column<DateTime>(nullable: true),
            done_at      = t.Column<DateTime>(nullable: true),
            created_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_kitchen_tickets", x => x.id);
            t.ForeignKey("fk_kt_stations", x => x.station_id, "kitchen_stations", "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_kt_orders",   x => x.order_id,   "dine_in_orders",   "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateIndex("ix_kt_station_status", "kitchen_tickets", new[] {"station_id", "status"});

        m.CreateTable("kitchen_ticket_items", t => new
        {
            id            = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id     = t.Column<Guid>(nullable: false),
            ticket_id     = t.Column<Guid>(nullable: false),
            order_item_id = t.Column<Guid>(nullable: false),
            item_name     = t.Column<string>(maxLength: 200, nullable: false),
            quantity      = t.Column<int>(nullable: false),
            notes         = t.Column<string>(maxLength: 200, nullable: true),
            modifiers     = t.Column<string>(nullable: true),   // JSON
            status        = t.Column<int>(nullable: false, defaultValue: 0),
            created_at    = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at    = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_kitchen_ticket_items", x => x.id);
            t.ForeignKey("fk_kti_tickets", x => x.ticket_id, "kitchen_tickets", "id", onDelete: ReferentialAction.Cascade);
        });

        // ── Table Reservations ────────────────────────────────────────────
        m.CreateTable("table_reservations", t => new
        {
            id             = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id      = t.Column<Guid>(nullable: false),
            branch_id      = t.Column<Guid>(nullable: false),
            table_id       = t.Column<Guid>(nullable: true),
            customer_name  = t.Column<string>(maxLength: 100, nullable: false),
            customer_phone = t.Column<string>(maxLength: 20, nullable: false),
            guest_count    = t.Column<int>(nullable: false, defaultValue: 1),
            reserved_at    = t.Column<DateTime>(nullable: false),
            duration_min   = t.Column<int>(nullable: false, defaultValue: 90),
            status         = t.Column<int>(nullable: false, defaultValue: 0),  // Pending
            notes          = t.Column<string>(maxLength: 500, nullable: true),
            confirmed_at   = t.Column<DateTime>(nullable: true),
            seated_at      = t.Column<DateTime>(nullable: true),
            created_at     = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at     = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_table_reservations", x => x.id);
            t.ForeignKey("fk_tr_branches", x => x.branch_id, "branches",          "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_tr_tables",   x => x.table_id,  "restaurant_tables", "id", onDelete: ReferentialAction.SetNull);
        });

        m.CreateIndex("ix_tr_branch_time", "table_reservations", new[] {"branch_id", "reserved_at"});

        // ══════════════════════════════════════════════════════════════════
        //  7. LICENSING
        // ══════════════════════════════════════════════════════════════════
        m.CreateTable("plans", t => new
        {
            id                      = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id               = t.Column<Guid>(nullable: false, defaultValue: Guid.Empty),
            name                    = t.Column<string>(maxLength: 100, nullable: false),
            name_en                 = t.Column<string>(maxLength: 100, nullable: false),
            description             = t.Column<string>(maxLength: 500, nullable: true),
            type                    = t.Column<short>(nullable: false),
            is_active               = t.Column<bool>(nullable: false, defaultValue: true),
            sort_order              = t.Column<int>(nullable: false, defaultValue: 0),
            monthly_price           = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            annual_price            = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            lifetime_price          = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            setup_fee               = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            max_branches            = t.Column<int>(nullable: false, defaultValue: 1),
            max_users               = t.Column<int>(nullable: false, defaultValue: 5),
            max_products            = t.Column<int>(nullable: false, defaultValue: 100),
            max_customers           = t.Column<int>(nullable: false, defaultValue: -1),
            max_orders              = t.Column<int>(nullable: false, defaultValue: -1),
            data_retention_days     = t.Column<int>(nullable: false, defaultValue: 365),
            has_pos                 = t.Column<bool>(nullable: false, defaultValue: true),
            has_inventory           = t.Column<bool>(nullable: false, defaultValue: true),
            has_sync_engine         = t.Column<bool>(nullable: false, defaultValue: false),
            has_multi_branch        = t.Column<bool>(nullable: false, defaultValue: false),
            has_advanced_reports    = t.Column<bool>(nullable: false, defaultValue: false),
            has_api_access          = t.Column<bool>(nullable: false, defaultValue: false),
            has_white_label         = t.Column<bool>(nullable: false, defaultValue: false),
            has_ai                  = t.Column<bool>(nullable: false, defaultValue: false),
            has_delivery            = t.Column<bool>(nullable: false, defaultValue: false),
            has_whatsapp            = t.Column<bool>(nullable: false, defaultValue: false),
            max_whatsapp_numbers    = t.Column<int>(nullable: false, defaultValue: 0),
            has_restaurant_module   = t.Column<bool>(nullable: false, defaultValue: false),
            max_restaurant_tables   = t.Column<int>(nullable: false, defaultValue: 0),
            has_kds                 = t.Column<bool>(nullable: false, defaultValue: false),
            has_qr_menu             = t.Column<bool>(nullable: false, defaultValue: false),
            has_table_reservations  = t.Column<bool>(nullable: false, defaultValue: false),
            has_restaurant_analytics = t.Column<bool>(nullable: false, defaultValue: false),
            has_rental_module       = t.Column<bool>(nullable: false, defaultValue: false),
            max_rental_assets       = t.Column<int>(nullable: false, defaultValue: 0),
            has_mall_module         = t.Column<bool>(nullable: false, defaultValue: false),
            has_loyalty_program     = t.Column<bool>(nullable: false, defaultValue: false),
            has_wallet              = t.Column<bool>(nullable: false, defaultValue: false),
            support_level           = t.Column<string>(maxLength: 50, nullable: false, defaultValue: "email"),
            uptime_sla_percent      = t.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 99m),
            created_at              = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at              = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t => t.PrimaryKey("pk_plans", x => x.id));

        m.CreateTable("subscriptions", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            plan_id     = t.Column<Guid>(nullable: false),
            status      = t.Column<short>(nullable: false, defaultValue: (short)0),
            cycle       = t.Column<short>(nullable: false, defaultValue: (short)0),
            started_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            expires_at  = t.Column<DateTime>(nullable: false),
            cancelled_at = t.Column<DateTime>(nullable: true),
            price_paid  = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            payment_ref = t.Column<string>(maxLength: 200, nullable: true),
            notes       = t.Column<string>(maxLength: 500, nullable: true),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_subscriptions", x => x.id);
            t.ForeignKey("fk_subs_tenants", x => x.tenant_id, "tenants", "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_subs_plans",   x => x.plan_id,   "plans",   "id");
        });

        m.CreateTable("licenses", t => new
        {
            id           = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id    = t.Column<Guid>(nullable: false),
            sub_id       = t.Column<Guid>(nullable: false),
            license_key  = t.Column<string>(maxLength: 100, nullable: false),
            status       = t.Column<short>(nullable: false, defaultValue: (short)0),
            device_limit = t.Column<int>(nullable: false, defaultValue: 5),
            activated_at = t.Column<DateTime>(nullable: true),
            expires_at   = t.Column<DateTime>(nullable: false),
            created_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_licenses", x => x.id);
            t.ForeignKey("fk_lic_tenants", x => x.tenant_id, "tenants",       "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_lic_subs",    x => x.sub_id,    "subscriptions", "id");
        });

        m.CreateIndex("ix_licenses_key", "licenses", "license_key", unique: true);

        m.CreateTable("device_registrations", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            license_id  = t.Column<Guid>(nullable: false),
            device_id   = t.Column<string>(maxLength: 200, nullable: false),
            device_name = t.Column<string>(maxLength: 200, nullable: true),
            platform    = t.Column<string>(maxLength: 50, nullable: true),
            is_active   = t.Column<bool>(nullable: false, defaultValue: true),
            last_seen_at = t.Column<DateTime>(nullable: true),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_device_registrations", x => x.id);
            t.ForeignKey("fk_dr_licenses", x => x.license_id, "licenses", "id", onDelete: ReferentialAction.Cascade);
        });

        // ══════════════════════════════════════════════════════════════════
        //  8. PLUGINS / MARKETPLACE
        // ══════════════════════════════════════════════════════════════════
        m.CreateTable("plugins", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false, defaultValue: Guid.Empty),
            name        = t.Column<string>(maxLength: 200, nullable: false),
            slug        = t.Column<string>(maxLength: 100, nullable: false),
            description = t.Column<string>(maxLength: 2000, nullable: true),
            version     = t.Column<string>(maxLength: 20, nullable: false),
            price       = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            is_free     = t.Column<bool>(nullable: false, defaultValue: true),
            is_active   = t.Column<bool>(nullable: false, defaultValue: true),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t => t.PrimaryKey("pk_plugins", x => x.id));

        m.CreateTable("plugin_installations", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            plugin_id   = t.Column<Guid>(nullable: false),
            is_active   = t.Column<bool>(nullable: false, defaultValue: true),
            config      = t.Column<string>(nullable: true),
            installed_at = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_plugin_installations", x => x.id);
            t.ForeignKey("fk_pi_tenants", x => x.tenant_id, "tenants", "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_pi_plugins", x => x.plugin_id, "plugins", "id");
        });

        // ══════════════════════════════════════════════════════════════════
        //  9. RENTAL MODULE
        // ══════════════════════════════════════════════════════════════════
        m.CreateTable("rental_assets", t => new
        {
            id              = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id       = t.Column<Guid>(nullable: false),
            branch_id       = t.Column<Guid>(nullable: true),
            name            = t.Column<string>(maxLength: 200, nullable: false),
            name_en         = t.Column<string>(maxLength: 200, nullable: true),
            description     = t.Column<string>(maxLength: 2000, nullable: true),
            category        = t.Column<string>(maxLength: 100, nullable: true),
            daily_rate      = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            deposit_amount  = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            is_available    = t.Column<bool>(nullable: false, defaultValue: true),
            is_active       = t.Column<bool>(nullable: false, defaultValue: true),
            created_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_rental_assets", x => x.id);
            t.ForeignKey("fk_ra_tenants", x => x.tenant_id, "tenants", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("rental_asset_images", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            asset_id    = t.Column<Guid>(nullable: false),
            url         = t.Column<string>(maxLength: 500, nullable: false),
            is_primary  = t.Column<bool>(nullable: false, defaultValue: false),
            sort_order  = t.Column<int>(nullable: false, defaultValue: 0),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_rental_images", x => x.id);
            t.ForeignKey("fk_ri_assets", x => x.asset_id, "rental_assets", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("rental_bookings", t => new
        {
            id              = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id       = t.Column<Guid>(nullable: false),
            customer_id     = t.Column<Guid>(nullable: true),
            customer_name   = t.Column<string>(maxLength: 100, nullable: false),
            customer_phone  = t.Column<string>(maxLength: 20, nullable: false),
            booking_number  = t.Column<string>(maxLength: 50, nullable: false),
            status          = t.Column<short>(nullable: false, defaultValue: (short)0),
            starts_at       = t.Column<DateTime>(nullable: false),
            ends_at         = t.Column<DateTime>(nullable: false),
            total           = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            deposit_paid    = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            notes           = t.Column<string>(maxLength: 1000, nullable: true),
            created_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_rental_bookings", x => x.id);
            t.ForeignKey("fk_rb_tenants", x => x.tenant_id, "tenants", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("rental_booking_items", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            booking_id  = t.Column<Guid>(nullable: false),
            asset_id    = t.Column<Guid>(nullable: false),
            days        = t.Column<int>(nullable: false),
            daily_rate  = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            total       = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_rental_booking_items", x => x.id);
            t.ForeignKey("fk_rbi_bookings", x => x.booking_id, "rental_bookings", "id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("fk_rbi_assets",   x => x.asset_id,   "rental_assets",   "id");
        });

        m.CreateTable("rental_payments", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            booking_id  = t.Column<Guid>(nullable: false),
            amount      = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            method      = t.Column<short>(nullable: false, defaultValue: (short)0),
            reference   = t.Column<string>(maxLength: 100, nullable: true),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_rental_payments", x => x.id);
            t.ForeignKey("fk_rp_bookings", x => x.booking_id, "rental_bookings", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("damage_reports", t => new
        {
            id              = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id       = t.Column<Guid>(nullable: false),
            booking_id      = t.Column<Guid>(nullable: false),
            asset_id        = t.Column<Guid>(nullable: false),
            description     = t.Column<string>(maxLength: 2000, nullable: false),
            repair_cost     = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            photo_urls      = t.Column<string>(nullable: true),
            resolved        = t.Column<bool>(nullable: false, defaultValue: false),
            created_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_damage_reports", x => x.id);
            t.ForeignKey("fk_dr_bookings", x => x.booking_id, "rental_bookings", "id");
            t.ForeignKey("fk_dr_assets",   x => x.asset_id,   "rental_assets",   "id");
        });

        m.CreateTable("maintenance_logs", t => new
        {
            id              = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id       = t.Column<Guid>(nullable: false),
            asset_id        = t.Column<Guid>(nullable: false),
            description     = t.Column<string>(maxLength: 2000, nullable: false),
            cost            = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            performed_at    = t.Column<DateTime>(nullable: false),
            next_due_at     = t.Column<DateTime>(nullable: true),
            created_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_maintenance_logs", x => x.id);
            t.ForeignKey("fk_ml_assets", x => x.asset_id, "rental_assets", "id", onDelete: ReferentialAction.Cascade);
        });

        // ══════════════════════════════════════════════════════════════════
        //  10. SYNC ENGINE (Offline support)
        // ══════════════════════════════════════════════════════════════════
        m.CreateTable("sync_queues", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            branch_id   = t.Column<Guid>(nullable: false),
            device_id   = t.Column<string>(maxLength: 200, nullable: false),
            entity      = t.Column<string>(maxLength: 100, nullable: false),
            operation   = t.Column<string>(maxLength: 20, nullable: false),
            payload     = t.Column<string>(nullable: false),
            status      = t.Column<short>(nullable: false, defaultValue: (short)0),
            attempts    = t.Column<int>(nullable: false, defaultValue: 0),
            error_msg   = t.Column<string>(maxLength: 500, nullable: true),
            processed_at = t.Column<DateTime>(nullable: true),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_sync_queues", x => x.id);
            t.ForeignKey("fk_sq_tenants", x => x.tenant_id, "tenants", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateIndex("ix_sq_status_created", "sync_queues", new[] {"status", "created_at"});

        m.CreateTable("sync_sessions", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            device_id   = t.Column<string>(maxLength: 200, nullable: false),
            started_at  = t.Column<DateTime>(nullable: false),
            completed_at = t.Column<DateTime>(nullable: true),
            records_synced = t.Column<int>(nullable: false, defaultValue: 0),
            errors_count   = t.Column<int>(nullable: false, defaultValue: 0),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t => t.PrimaryKey("pk_sync_sessions", x => x.id));

        m.CreateTable("sync_logs", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            session_id  = t.Column<Guid>(nullable: false),
            entity      = t.Column<string>(maxLength: 100, nullable: false),
            operation   = t.Column<string>(maxLength: 20, nullable: false),
            entity_id   = t.Column<string>(maxLength: 100, nullable: false),
            success     = t.Column<bool>(nullable: false),
            error_msg   = t.Column<string>(maxLength: 500, nullable: true),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_sync_logs", x => x.id);
            t.ForeignKey("fk_sl_sessions", x => x.session_id, "sync_sessions", "id", onDelete: ReferentialAction.Cascade);
        });

        // ══════════════════════════════════════════════════════════════════
        //  11. MALL MODULE (من MallX)
        // ══════════════════════════════════════════════════════════════════
        m.CreateTable("malls", t => new
        {
            id              = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id       = t.Column<Guid>(nullable: false),
            name            = t.Column<string>(maxLength: 200, nullable: false),
            name_en         = t.Column<string>(maxLength: 200, nullable: true),
            slug            = t.Column<string>(maxLength: 100, nullable: false),
            logo_url        = t.Column<string>(maxLength: 500, nullable: true),
            commission_rate = t.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 0.05m),
            is_active       = t.Column<bool>(nullable: false, defaultValue: true),
            created_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_malls", x => x.id);
            t.ForeignKey("fk_malls_tenants", x => x.tenant_id, "tenants", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateIndex("ix_malls_slug", "malls", "slug", unique: true);

        m.CreateTable("mall_stores", t => new
        {
            id              = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id       = t.Column<Guid>(nullable: false),
            mall_id         = t.Column<Guid>(nullable: false),
            name            = t.Column<string>(maxLength: 200, nullable: false),
            name_en         = t.Column<string>(maxLength: 200, nullable: true),
            store_type      = t.Column<string>(maxLength: 50, nullable: false),
            floor_number    = t.Column<int>(nullable: false, defaultValue: 1),
            unit_number     = t.Column<string>(maxLength: 50, nullable: true),
            logo_url        = t.Column<string>(maxLength: 500, nullable: true),
            commission_rate = t.Column<decimal>(type: "decimal(5,4)", nullable: false, defaultValue: 0.05m),
            is_open         = t.Column<bool>(nullable: false, defaultValue: false),
            is_active       = t.Column<bool>(nullable: false, defaultValue: true),
            created_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_mall_stores", x => x.id);
            t.ForeignKey("fk_ms_malls", x => x.mall_id, "malls", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("mall_customers", t => new
        {
            id              = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id       = t.Column<Guid>(nullable: false),
            mall_id         = t.Column<Guid>(nullable: false),
            first_name      = t.Column<string>(maxLength: 100, nullable: false),
            last_name       = t.Column<string>(maxLength: 100, nullable: false),
            email           = t.Column<string>(maxLength: 200, nullable: false),
            phone           = t.Column<string>(maxLength: 20, nullable: false),
            password_hash   = t.Column<string>(maxLength: 500, nullable: false),
            fcm_token       = t.Column<string>(maxLength: 500, nullable: true),
            wallet_balance  = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            loyalty_points  = t.Column<int>(nullable: false, defaultValue: 0),
            referral_code   = t.Column<string>(maxLength: 20, nullable: true),
            is_active       = t.Column<bool>(nullable: false, defaultValue: true),
            created_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_mall_customers", x => x.id);
            t.ForeignKey("fk_mc_malls", x => x.mall_id, "malls", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateIndex("ix_mc_email_mall", "mall_customers", new[] {"email", "mall_id"}, unique: true);

        m.CreateTable("mall_products", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            store_id    = t.Column<Guid>(nullable: false),
            name        = t.Column<string>(maxLength: 200, nullable: false),
            name_en     = t.Column<string>(maxLength: 200, nullable: true),
            price       = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            image_url   = t.Column<string>(maxLength: 500, nullable: true),
            is_available = t.Column<bool>(nullable: false, defaultValue: true),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_mall_products", x => x.id);
            t.ForeignKey("fk_mp_stores", x => x.store_id, "mall_stores", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("customer_addresses", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            customer_id = t.Column<Guid>(nullable: false),
            label       = t.Column<string>(maxLength: 50, nullable: false),
            street      = t.Column<string>(maxLength: 500, nullable: false),
            city        = t.Column<string>(maxLength: 100, nullable: true),
            lat         = t.Column<decimal>(type: "decimal(10,7)", nullable: true),
            lng         = t.Column<decimal>(type: "decimal(10,7)", nullable: true),
            is_default  = t.Column<bool>(nullable: false, defaultValue: false),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_customer_addresses", x => x.id);
            t.ForeignKey("fk_ca_customers", x => x.customer_id, "mall_customers", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("customer_refresh_tokens", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            customer_id = t.Column<Guid>(nullable: false),
            token_hash  = t.Column<string>(maxLength: 500, nullable: false),
            expires_at  = t.Column<DateTime>(nullable: false),
            is_revoked  = t.Column<bool>(nullable: false, defaultValue: false),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_customer_refresh_tokens", x => x.id);
            t.ForeignKey("fk_crt_customers", x => x.customer_id, "mall_customers", "id", onDelete: ReferentialAction.Cascade);
        });

        // Mall Orders
        m.CreateTable("carts", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            customer_id = t.Column<Guid>(nullable: false),
            mall_id     = t.Column<Guid>(nullable: false),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_carts", x => x.id);
            t.ForeignKey("fk_carts_customers", x => x.customer_id, "mall_customers", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("cart_items", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            cart_id     = t.Column<Guid>(nullable: false),
            product_id  = t.Column<Guid>(nullable: false),
            quantity    = t.Column<int>(nullable: false, defaultValue: 1),
            unit_price  = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_cart_items", x => x.id);
            t.ForeignKey("fk_ci_carts", x => x.cart_id, "carts", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("mall_orders", t => new
        {
            id              = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id       = t.Column<Guid>(nullable: false),
            mall_id         = t.Column<Guid>(nullable: false),
            customer_id     = t.Column<Guid>(nullable: false),
            order_number    = t.Column<string>(maxLength: 20, nullable: false),
            status          = t.Column<string>(maxLength: 50, nullable: false, defaultValue: "Placed"),
            total           = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            delivery_fee    = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            discount        = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            notes           = t.Column<string>(maxLength: 500, nullable: true),
            created_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_mall_orders", x => x.id);
            t.ForeignKey("fk_mo_malls",      x => x.mall_id,     "malls",          "id");
            t.ForeignKey("fk_mo_customers",  x => x.customer_id, "mall_customers", "id");
        });

        m.CreateIndex("ix_mo_customer", "mall_orders", "customer_id");
        m.CreateIndex("ix_mo_number",   "mall_orders", new[] {"order_number", "tenant_id"}, unique: true);

        m.CreateTable("store_order_items", t => new
        {
            id              = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id       = t.Column<Guid>(nullable: false),
            mall_order_id   = t.Column<Guid>(nullable: false),
            store_id        = t.Column<Guid>(nullable: false),
            product_id      = t.Column<Guid>(nullable: false),
            product_name    = t.Column<string>(maxLength: 200, nullable: false),
            quantity        = t.Column<int>(nullable: false),
            unit_price      = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            subtotal        = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            created_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_store_order_items", x => x.id);
            t.ForeignKey("fk_soi_mall_orders", x => x.mall_order_id, "mall_orders", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("payment_transactions", t => new
        {
            id              = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id       = t.Column<Guid>(nullable: false),
            mall_order_id   = t.Column<Guid>(nullable: true),
            gateway         = t.Column<string>(maxLength: 50, nullable: false),
            gateway_ref     = t.Column<string>(maxLength: 200, nullable: true),
            amount          = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            status          = t.Column<string>(maxLength: 50, nullable: false),
            raw_response    = t.Column<string>(nullable: true),
            created_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t => t.PrimaryKey("pk_payment_transactions", x => x.id));

        m.CreateTable("drivers", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            name        = t.Column<string>(maxLength: 100, nullable: false),
            phone       = t.Column<string>(maxLength: 20, nullable: false),
            status      = t.Column<string>(maxLength: 50, nullable: false, defaultValue: "Offline"),
            current_lat = t.Column<decimal>(type: "decimal(10,7)", nullable: true),
            current_lng = t.Column<decimal>(type: "decimal(10,7)", nullable: true),
            is_active   = t.Column<bool>(nullable: false, defaultValue: true),
            fcm_token   = t.Column<string>(maxLength: 500, nullable: true),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_drivers", x => x.id);
            t.ForeignKey("fk_drivers_tenants", x => x.tenant_id, "tenants", "id", onDelete: ReferentialAction.Cascade);
        });

        // ── Loyalty & Wallet ──────────────────────────────────────────────
        m.CreateTable("loyalty_accounts", t => new
        {
            id              = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id       = t.Column<Guid>(nullable: false),
            customer_id     = t.Column<Guid>(nullable: false),
            total_points    = t.Column<int>(nullable: false, defaultValue: 0),
            lifetime_points = t.Column<int>(nullable: false, defaultValue: 0),
            tier            = t.Column<int>(nullable: false, defaultValue: 0),
            created_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_loyalty_accounts", x => x.id);
            t.ForeignKey("fk_la_customers", x => x.customer_id, "mall_customers", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("wallets", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            customer_id = t.Column<Guid>(nullable: false),
            balance     = t.Column<decimal>(type: "decimal(12,2)", nullable: false, defaultValue: 0m),
            currency    = t.Column<string>(maxLength: 3, nullable: false, defaultValue: "EGP"),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_wallets", x => x.id);
            t.ForeignKey("fk_w_customers", x => x.customer_id, "mall_customers", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateIndex("ix_wallets_customer", "wallets", "customer_id", unique: true);

        m.CreateTable("wallet_transactions", t => new
        {
            id           = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id    = t.Column<Guid>(nullable: false),
            wallet_id    = t.Column<Guid>(nullable: false),
            type         = t.Column<string>(maxLength: 50, nullable: false),
            amount       = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            balance_after = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            reference    = t.Column<string>(maxLength: 200, nullable: true),
            note         = t.Column<string>(maxLength: 500, nullable: true),
            created_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at   = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_wallet_transactions", x => x.id);
            t.ForeignKey("fk_wt_wallets", x => x.wallet_id, "wallets", "id", onDelete: ReferentialAction.Cascade);
        });

        m.CreateTable("commission_settlements", t => new
        {
            id              = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id       = t.Column<Guid>(nullable: false),
            store_id        = t.Column<Guid>(nullable: false),
            mall_order_id   = t.Column<Guid>(nullable: false),
            order_amount    = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            commission_rate = t.Column<decimal>(type: "decimal(5,4)", nullable: false),
            commission_amt  = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            status          = t.Column<string>(maxLength: 50, nullable: false, defaultValue: "Pending"),
            settled_at      = t.Column<DateTime>(nullable: true),
            created_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at      = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t => t.PrimaryKey("pk_commission_settlements", x => x.id));

        m.CreateTable("marketplace_listings", t => new
        {
            id          = t.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
            tenant_id   = t.Column<Guid>(nullable: false),
            asset_id    = t.Column<Guid>(nullable: false),
            title       = t.Column<string>(maxLength: 200, nullable: false),
            description = t.Column<string>(maxLength: 2000, nullable: true),
            price       = t.Column<decimal>(type: "decimal(12,2)", nullable: false),
            is_active   = t.Column<bool>(nullable: false, defaultValue: true),
            created_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
            updated_at  = t.Column<DateTime>(nullable: false, defaultValueSql: "now()")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_marketplace_listings", x => x.id);
            t.ForeignKey("fk_ml_assets", x => x.asset_id, "rental_assets", "id", onDelete: ReferentialAction.Cascade);
        });

        // ══════════════════════════════════════════════════════════════════
        //  SEED DATA — Plans + Platform Owner Tenant
        // ══════════════════════════════════════════════════════════════════
        var platformTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        m.InsertData("tenants", new[]
        {
            "id", "name", "name_en", "slug", "business_type", "status", "modules",
            "currency", "is_active", "created_at", "updated_at"
        }, new object[,]
        {
            {
                platformTenantId, "OmniX Platform", "OmniX Platform", "omnix-platform",
                (short)0, (short)1, int.MaxValue,
                "EGP", true, DateTime.UtcNow, DateTime.UtcNow
            }
        });

        // Plans — Trial, Basic, Professional, Enterprise
        m.InsertData("plans", new[]
        {
            "id", "tenant_id", "name", "name_en", "type", "is_active", "sort_order",
            "monthly_price", "annual_price", "max_branches", "max_users", "max_products",
            "has_pos", "has_inventory", "has_restaurant_module", "has_kds", "has_qr_menu",
            "has_table_reservations", "has_delivery", "has_whatsapp", "max_whatsapp_numbers",
            "has_multi_branch", "has_ai", "has_loyalty_program", "has_wallet",
            "support_level", "uptime_sla_percent", "data_retention_days",
            "created_at", "updated_at"
        }, new object[,]
        {
            {
                Guid.NewGuid(), platformTenantId, "تجريبي", "Trial", (short)0, true, 0,
                0m, 0m, 1, 2, 50, true, true, false, false, false, false, false, false, 0,
                false, false, false, false, "email", 99m, 30, DateTime.UtcNow, DateTime.UtcNow
            },
            {
                Guid.NewGuid(), platformTenantId, "أساسي", "Basic", (short)1, true, 1,
                199m, 1990m, 3, 10, 500, true, true, true, false, true, false, true, true, 1,
                true, false, false, false, "email", 99m, 365, DateTime.UtcNow, DateTime.UtcNow
            },
            {
                Guid.NewGuid(), platformTenantId, "احترافي", "Professional", (short)2, true, 2,
                499m, 4990m, 10, 30, 2000, true, true, true, true, true, true, true, true, 3,
                true, true, true, true, "priority", 99m, 730, DateTime.UtcNow, DateTime.UtcNow
            },
            {
                Guid.NewGuid(), platformTenantId, "مؤسسي", "Enterprise", (short)4, true, 3,
                999m, 9990m, -1, -1, -1, true, true, true, true, true, true, true, true, -1,
                true, true, true, true, "dedicated", 99m, -1, DateTime.UtcNow, DateTime.UtcNow
            }
        });
    }

    protected override void Down(MigrationBuilder m)
    {
        // Drop in reverse dependency order
        m.DropTable("marketplace_listings");
        m.DropTable("commission_settlements");
        m.DropTable("wallet_transactions");
        m.DropTable("wallets");
        m.DropTable("loyalty_accounts");
        m.DropTable("drivers");
        m.DropTable("payment_transactions");
        m.DropTable("store_order_items");
        m.DropTable("mall_orders");
        m.DropTable("cart_items");
        m.DropTable("carts");
        m.DropTable("customer_refresh_tokens");
        m.DropTable("customer_addresses");
        m.DropTable("mall_products");
        m.DropTable("mall_customers");
        m.DropTable("mall_stores");
        m.DropTable("malls");
        m.DropTable("sync_logs");
        m.DropTable("sync_sessions");
        m.DropTable("sync_queues");
        m.DropTable("maintenance_logs");
        m.DropTable("damage_reports");
        m.DropTable("rental_payments");
        m.DropTable("rental_booking_items");
        m.DropTable("rental_bookings");
        m.DropTable("rental_asset_images");
        m.DropTable("rental_assets");
        m.DropTable("plugin_installations");
        m.DropTable("plugins");
        m.DropTable("device_registrations");
        m.DropTable("licenses");
        m.DropTable("subscriptions");
        m.DropTable("plans");
        m.DropTable("kitchen_ticket_items");
        m.DropTable("kitchen_tickets");
        m.DropTable("order_item_modifiers");
        m.DropTable("dine_in_order_items");
        m.DropTable("dine_in_orders");
        m.DropTable("kitchen_stations");
        m.DropTable("table_reservations");
        m.DropTable("session_payments");
        m.DropTable("table_sessions");
        m.DropTable("restaurant_tables");
        m.DropTable("floor_plans");
        m.DropTable("branch_item_availability");
        m.DropTable("branch_menu_overrides");
        m.DropTable("menu_modifiers");
        m.DropTable("menu_modifier_groups");
        m.DropTable("menu_item_variants");
        m.DropTable("restaurant_menu_items");
        m.DropTable("restaurant_categories");
        m.DropTable("expenses");
        m.DropTable("payments");
        m.DropTable("purchase_order_items");
        m.DropTable("purchase_orders");
        m.DropTable("sale_order_items");
        m.DropTable("sale_orders");
        m.DropTable("stock_movements");
        m.DropTable("stock_items");
        m.DropTable("suppliers");
        m.DropTable("customers");
        m.DropTable("products");
        m.DropTable("categories");
        m.DropTable("audit_logs");
        m.DropTable("feature_flags");
        m.DropTable("refresh_tokens");
        m.DropTable("application_permissions");
        m.DropTable("application_users");
        m.DropTable("application_roles");
        m.DropTable("branches");
        m.DropTable("tenants");
    }
}
