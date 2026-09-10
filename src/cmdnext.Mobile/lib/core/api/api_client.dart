import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../storage/app_storage.dart';
import 'api_exception.dart';

/// Thin wrapper over Dio. Every feature repository goes through this so that
/// base URL, auth header, and error translation live in exactly one place.
class ApiClient {
  const ApiClient(this._dio);

  final Dio _dio;

  Dio get raw => _dio;

  Future<T> get<T>(
    String path, {
    Map<String, dynamic>? query,
    CancelToken? cancelToken,
  }) => _send<T>(
    () => _dio.get<T>(path, queryParameters: query, cancelToken: cancelToken),
  );

  Future<T> post<T>(
    String path, {
    Object? body,
    Map<String, dynamic>? query,
    CancelToken? cancelToken,
  }) => _send<T>(
    () => _dio.post<T>(
      path,
      data: body,
      queryParameters: query,
      cancelToken: cancelToken,
    ),
  );

  Future<T> put<T>(
    String path, {
    Object? body,
    Map<String, dynamic>? query,
    CancelToken? cancelToken,
  }) => _send<T>(
    () => _dio.put<T>(
      path,
      data: body,
      queryParameters: query,
      cancelToken: cancelToken,
    ),
  );

  Future<T> delete<T>(
    String path, {
    Map<String, dynamic>? query,
    CancelToken? cancelToken,
  }) => _send<T>(
    () =>
        _dio.delete<T>(path, queryParameters: query, cancelToken: cancelToken),
  );

  /// Runs a request, turning transport failures into [ApiException] so no
  /// `DioException` ever escapes into feature code or the UI.
  Future<T> _send<T>(Future<Response<T>> Function() request) async {
    try {
      final response = await request();
      return response.data as T;
    } on DioException catch (e) {
      throw ApiException.from(e);
    }
  }

  /// Lists are returned bare by the API; this keeps the cast in one place.
  Future<List<Map<String, dynamic>>> getList(
    String path, {
    Map<String, dynamic>? query,
    CancelToken? cancelToken,
  }) async {
    final data = await get<List<dynamic>?>(
      path,
      query: query,
      cancelToken: cancelToken,
    );
    return (data ?? const [])
        .whereType<Map>()
        .map((e) => e.cast<String, dynamic>())
        .toList();
  }

  Future<Map<String, dynamic>> getObject(
    String path, {
    Map<String, dynamic>? query,
    CancelToken? cancelToken,
  }) async {
    final data = await get<Map<String, dynamic>?>(
      path,
      query: query,
      cancelToken: cancelToken,
    );
    return data ?? const {};
  }
}

/// Builds the Dio instance. Rebuilt whenever the server URL changes so a new
/// origin takes effect immediately.
final dioProvider = Provider<Dio>((ref) {
  final origin = ref.watch(serverUrlProvider);
  final tokens = ref.watch(secureTokenStoreProvider);

  final dio = Dio(
    BaseOptions(
      // The API is versioned: `api/v{version}/[controller]`.
      baseUrl: '$origin/api/v1/',
      connectTimeout: const Duration(seconds: 12),
      receiveTimeout: const Duration(seconds: 30),
      sendTimeout: const Duration(seconds: 30),
      responseType: ResponseType.json,
      headers: {'Accept': 'application/json'},
      // 401 is handled as a real error so the interceptor can end the session.
      validateStatus: (s) => s != null && s >= 200 && s < 300,
    ),
  );

  dio.interceptors.add(
    InterceptorsWrapper(
      onRequest: (options, handler) async {
        // Login and register must not carry a stale token.
        final anonymous =
            options.path.contains('auth/login') ||
            options.path.contains('auth/register');
        if (!anonymous) {
          final token = await tokens.readToken();
          if (token != null && token.isNotEmpty) {
            options.headers['Authorization'] = 'Bearer $token';
          }
        }
        handler.next(options);
      },
      onError: (e, handler) {
        if (e.response?.statusCode == 401) {
          // Surface it so the session can be cleared; the listener is
          // registered by the auth controller to avoid a provider cycle.
          ref.read(unauthorizedSignalProvider).notify();
        }
        handler.next(e);
      },
    ),
  );

  return dio;
});

/// Decouples "the API said 401" from the auth controller. A direct
/// `ref.read(authProvider)` inside the Dio provider would be circular, since
/// the auth controller itself depends on the client.
class UnauthorizedSignal {
  final _listeners = <void Function()>[];

  void addListener(void Function() fn) => _listeners.add(fn);
  void removeListener(void Function() fn) => _listeners.remove(fn);

  void notify() {
    for (final fn in List.of(_listeners)) {
      fn();
    }
  }
}

final unauthorizedSignalProvider = Provider<UnauthorizedSignal>(
  (ref) => UnauthorizedSignal(),
);

final apiClientProvider = Provider<ApiClient>(
  (ref) => ApiClient(ref.watch(dioProvider)),
);
