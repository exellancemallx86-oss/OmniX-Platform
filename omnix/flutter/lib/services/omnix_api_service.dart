// ════════════════════════════════════════════════════════════════════════════
//  OmniX Flutter — API Service Layer
//  يحدّث كل الـ endpoints للـ OmniX unified backend
//  تحديث: استبدل YOUR_RENDER_URL بالـ URL الفعلي من Render
// ════════════════════════════════════════════════════════════════════════════

import 'dart:convert';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:http/http.dart' as http;

// ─── Config ──────────────────────────────────────────────────────────────
class OmniXConfig {
  // ← عدّل هذا بالـ URL الفعلي من Render بعد النشر
  static const String baseUrl = 'https://omnix-api.onrender.com';
  static const String apiUrl  = '$baseUrl/api';
  static const String signalRUrl = baseUrl;

  // API Endpoints
  static const String authLogin       = '$apiUrl/auth/login';
  static const String authRegister    = '$apiUrl/auth/register';
  static const String authRefresh     = '$apiUrl/auth/refresh';
  static const String authLogout      = '$apiUrl/auth/logout';
  static const String authMe          = '$apiUrl/auth/me';
  static const String authMfaSetup    = '$apiUrl/auth/mfa/setup';
  static const String authMfaVerify   = '$apiUrl/auth/mfa/verify';

  static const String mallAuthLogin    = '$apiUrl/mall/auth/login';
  static const String mallAuthRegister = '$apiUrl/mall/auth/register';
  static const String mallAuthRefresh  = '$apiUrl/mall/auth/refresh';
  static const String mallAuthMe       = '$apiUrl/mall/auth/me';

  static const String posProducts     = '$apiUrl/pos/products';
  static const String posSale         = '$apiUrl/pos/sale';
  static const String posSales        = '$apiUrl/pos/sales';

  static const String mallCart        = '$apiUrl/mall/cart';
  static const String mallOrders      = '$apiUrl/mall/orders';
  static const String mallCheckout    = '$apiUrl/mall/orders/checkout';

  static const String syncPush        = '$apiUrl/sync/push';
  static const String syncPull        = '$apiUrl/sync/pull';
  static const String syncStatus      = '$apiUrl/sync/status';

  static const String restaurantMenu  = '$apiUrl/restaurant/menu/items';
  static const String restaurantOrders = '$apiUrl/restaurant/orders';

  static const String rentalAssets    = '$apiUrl/rental/assets';
  static const String rentalBookings  = '$apiUrl/rental/bookings';

  static const String dashboard       = '$apiUrl/dashboard';
  static const String customers       = '$apiUrl/customers';
  static const String inventory       = '$apiUrl/inventory/products';
}

// ─── Storage Keys ─────────────────────────────────────────────────────────
class StorageKeys {
  static const String accessToken  = 'omnix_access_token';
  static const String refreshToken = 'omnix_refresh_token';
  static const String tokenType    = 'omnix_token_type';  // 'staff' | 'customer'
  static const String userId       = 'omnix_user_id';
  static const String tenantId     = 'omnix_tenant_id';
  static const String mallId       = 'omnix_mall_id';
  static const String onboarded    = 'omnix_onboarded';
}

// ─── API Client ───────────────────────────────────────────────────────────
class OmniXApiClient {
  static final _storage = FlutterSecureStorage();
  static final _client  = http.Client();

  // ── GET ──────────────────────────────────────────────────────────────
  static Future<ApiResult<T>> get<T>(
    String url, T Function(Map<String, dynamic>) fromJson, {
    Map<String, String>? params,
  }) async {
    try {
      final uri = params != null
          ? Uri.parse(url).replace(queryParameters: params)
          : Uri.parse(url);
      final headers = await _authHeaders();
      final res = await _client.get(uri, headers: headers)
          .timeout(const Duration(seconds: 30));
      return _handle(res, fromJson);
    } catch (e) {
      return ApiResult.error('خطأ في الاتصال: $e');
    }
  }

  // ── POST ─────────────────────────────────────────────────────────────
  static Future<ApiResult<T>> post<T>(
    String url, Map<String, dynamic> body,
    T Function(Map<String, dynamic>) fromJson, {
    bool auth = true,
  }) async {
    try {
      final headers = auth
          ? await _authHeaders()
          : {'Content-Type': 'application/json'};
      final res = await _client.post(
        Uri.parse(url),
        headers: headers,
        body: jsonEncode(body),
      ).timeout(const Duration(seconds: 30));
      return _handle(res, fromJson);
    } catch (e) {
      return ApiResult.error('خطأ في الاتصال: $e');
    }
  }

  // ── Token Management ─────────────────────────────────────────────────
  static Future<Map<String, String>> _authHeaders() async {
    var token = await _storage.read(key: StorageKeys.accessToken);

    // Auto-refresh if token missing
    if (token == null) {
      final refreshed = await _refreshToken();
      if (refreshed) {
        token = await _storage.read(key: StorageKeys.accessToken);
      }
    }

    return {
      'Content-Type': 'application/json',
      'Authorization': 'Bearer ${token ?? ''}',
    };
  }

  static Future<bool> _refreshToken() async {
    try {
      final refresh = await _storage.read(key: StorageKeys.refreshToken);
      if (refresh == null) return false;

      final tokenType = await _storage.read(key: StorageKeys.tokenType);
      final url = tokenType == 'customer'
          ? OmniXConfig.mallAuthRefresh
          : OmniXConfig.authRefresh;

      final res = await _client.post(
        Uri.parse(url),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'refreshToken': refresh}),
      );

      if (res.statusCode == 200) {
        final data = jsonDecode(res.body);
        await _storage.write(
            key: StorageKeys.accessToken,
            value: data['data']['accessToken']);
        await _storage.write(
            key: StorageKeys.refreshToken,
            value: data['data']['refreshToken']);
        return true;
      }
      return false;
    } catch (_) {
      return false;
    }
  }

  static Future<void> saveTokens({
    required String accessToken,
    required String refreshToken,
    required String tokenType,
    String? userId,
    String? tenantId,
    String? mallId,
  }) async {
    await _storage.write(key: StorageKeys.accessToken,  value: accessToken);
    await _storage.write(key: StorageKeys.refreshToken, value: refreshToken);
    await _storage.write(key: StorageKeys.tokenType,    value: tokenType);
    if (userId   != null) await _storage.write(key: StorageKeys.userId,   value: userId);
    if (tenantId != null) await _storage.write(key: StorageKeys.tenantId, value: tenantId);
    if (mallId   != null) await _storage.write(key: StorageKeys.mallId,   value: mallId);
  }

  static Future<void> clearTokens() async {
    await _storage.deleteAll();
  }

  // ── Response Handler ─────────────────────────────────────────────────
  static ApiResult<T> _handle<T>(
    http.Response res,
    T Function(Map<String, dynamic>) fromJson,
  ) {
    try {
      final body = jsonDecode(res.body) as Map<String, dynamic>;
      if (res.statusCode >= 200 && res.statusCode < 300) {
        if (body['success'] == true && body['data'] != null) {
          return ApiResult.ok(fromJson(body['data'] as Map<String, dynamic>));
        }
        return ApiResult.ok(fromJson(body));
      }
      return ApiResult.error(body['message'] ?? 'خطأ غير معروف');
    } catch (e) {
      return ApiResult.error('خطأ في تحليل الاستجابة: $e');
    }
  }
}

// ─── Result Wrapper ───────────────────────────────────────────────────────
class ApiResult<T> {
  final bool    success;
  final T?      data;
  final String? error;

  const ApiResult._({required this.success, this.data, this.error});

  factory ApiResult.ok(T data) => ApiResult._(success: true, data: data);
  factory ApiResult.error(String msg) => ApiResult._(success: false, error: msg);
}

// ─── pubspec.yaml dependencies (أضف هذه لـ pubspec.yaml) ────────────────
// dependencies:
//   flutter:
//     sdk: flutter
//   http: ^1.2.0
//   flutter_secure_storage: ^9.0.0
//   signalr_netcore: ^1.3.5     # SignalR للـ real-time
//   provider: ^6.1.2
//   go_router: ^13.0.0
//   cached_network_image: ^3.3.1
//   flutter_map: ^6.1.0         # GPS tracking
//   latlong2: ^0.9.0
//   intl: ^0.19.0
//   shimmer: ^3.0.0
//   qr_flutter: ^4.1.0          # QR codes
