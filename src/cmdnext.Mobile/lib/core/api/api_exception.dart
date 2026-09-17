import 'dart:io';

import 'package:dio/dio.dart';

/// A failure already translated into something worth showing a person.
///
/// The API returns `{ "message": "..." }` on handled errors (see
/// `AuthController`), so that is preferred over a status code when present.
class ApiException implements Exception {
  ApiException(this.message, {this.statusCode, this.kind});

  final String message;
  final int? statusCode;
  final ApiErrorKind? kind;

  bool get isUnauthorized => statusCode == 401;

  /// True when the server could not be reached at all — worth suggesting
  /// the private network, since that is the only route to it.
  bool get isUnreachable => kind == ApiErrorKind.network;

  @override
  String toString() => message;

  factory ApiException.from(DioException e) {
    switch (e.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
      case DioExceptionType.transformTimeout:
        return ApiException(
          'The server took too long to respond.',
          kind: ApiErrorKind.timeout,
        );

      case DioExceptionType.connectionError:
      case DioExceptionType.unknown:
        final inner = e.error;
        if (inner is SocketException || inner is HttpException) {
          return ApiException(
            'Cannot reach the server. Check your network connection.',
            kind: ApiErrorKind.network,
          );
        }
        return ApiException(
          'Cannot reach the server. Check your network connection.',
          kind: ApiErrorKind.network,
        );

      case DioExceptionType.cancel:
        return ApiException('Cancelled.', kind: ApiErrorKind.cancelled);

      case DioExceptionType.badCertificate:
        return ApiException(
          'The server certificate was rejected.',
          kind: ApiErrorKind.network,
        );

      case DioExceptionType.badResponse:
        final status = e.response?.statusCode;
        return ApiException(
          _messageFrom(e.response?.data) ?? _forStatus(status),
          statusCode: status,
          kind: status == 401
              ? ApiErrorKind.unauthorized
              : ApiErrorKind.server,
        );
    }
  }

  /// Digs the server's own message out of a JSON body, including the
  /// `{ errors: { field: [msg] } }` shape ASP.NET model validation produces.
  static String? _messageFrom(Object? data) {
    if (data is String && data.trim().isNotEmpty && data.length < 300) {
      return data.trim();
    }
    if (data is! Map) return null;

    for (final key in ['message', 'Message', 'title', 'detail', 'error']) {
      final v = data[key];
      if (v is String && v.trim().isNotEmpty) return v.trim();
    }

    final errors = data['errors'];
    if (errors is Map) {
      for (final v in errors.values) {
        if (v is List && v.isNotEmpty) return v.first.toString();
        if (v is String && v.isNotEmpty) return v;
      }
    }
    return null;
  }

  static String _forStatus(int? status) => switch (status) {
    400 => 'That request was not valid.',
    401 => 'Your session has expired. Please sign in again.',
    403 => 'You do not have access to that.',
    404 => 'Not found.',
    409 => 'That conflicts with something that already exists.',
    final s? when s >= 500 => 'The server hit an error. Try again shortly.',
    _ => 'Something went wrong.',
  };
}

enum ApiErrorKind { network, timeout, unauthorized, server, cancelled }
