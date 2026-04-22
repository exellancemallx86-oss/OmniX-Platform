# ════════════════════════════════════════════════════════════════════════════
#  OmniX Platform — Frontend Dockerfile
#  Next.js 14 + Node.js 20 Alpine + Standalone output + Dynamic PORT
# ════════════════════════════════════════════════════════════════════════════

# ── Stage 1: Dependencies ─────────────────────────────────────────────────
FROM node:20-alpine AS deps
WORKDIR /app

# Install libc6-compat for Alpine compatibility with some native modules
RUN apk add --no-cache libc6-compat

# Copy package files and install production + dev dependencies
# (dev deps are needed for the build step)
COPY frontend/package.json frontend/package-lock.json* ./
RUN npm ci --prefer-offline

# ── Stage 2: Build ────────────────────────────────────────────────────────
FROM node:20-alpine AS builder
WORKDIR /app

# Accept build-time env vars so Next.js can inline NEXT_PUBLIC_* values
ARG NEXT_PUBLIC_API_URL
ARG NEXT_PUBLIC_SIGNALR_URL
ARG NEXT_PUBLIC_MALL_SLUG

ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL
ENV NEXT_PUBLIC_SIGNALR_URL=$NEXT_PUBLIC_SIGNALR_URL
ENV NEXT_PUBLIC_MALL_SLUG=$NEXT_PUBLIC_MALL_SLUG

# Disable Next.js telemetry during build
ENV NEXT_TELEMETRY_DISABLED=1

# Copy installed node_modules from deps stage
COPY --from=deps /app/node_modules ./node_modules

# Copy all frontend source files
COPY frontend/ .

# Build the Next.js application (outputs to .next/standalone via next.config.js)
RUN npm run build

# ── Stage 3: Runtime ──────────────────────────────────────────────────────
FROM node:20-alpine AS runtime
WORKDIR /app

ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1

# Security: non-root user
RUN addgroup --system --gid 1001 nodejs \
 && adduser  --system --uid 1001 nextjs

# Copy the standalone build output (self-contained server bundle)
COPY --from=builder --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=builder --chown=nextjs:nodejs /app/.next/static     ./.next/static
COPY --from=builder --chown=nextjs:nodejs /app/public           ./public 2>/dev/null || true

USER nextjs

# Railway / Render provide PORT as an environment variable
EXPOSE 3000

# Health check — verifies the Next.js server is responding
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:${PORT:-3000}/ || exit 1

# Start the standalone Next.js server on the dynamic PORT
ENV PORT=3000
CMD ["sh", "-c", "node server.js"]
