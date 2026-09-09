import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_exception.dart';
import 'chat_models.dart';

/// Talks to `/ai`.
class ChatRepository {
  const ChatRepository(this._api);

  final ApiClient _api;

  Future<List<ChatSession>> sessions() async =>
      (await _api.getList('ai/sessions'))
          .map(ChatSession.fromJson)
          .toList(growable: false);

  Future<ChatSessionDetail> session(String id) async =>
      ChatSessionDetail.fromJson(await _api.getObject('ai/sessions/$id'));

  Future<ChatSession> createSession({String? title, String? spaceId}) async =>
      ChatSession.fromJson(
        await _api.post<Map<String, dynamic>>(
          'ai/sessions',
          body: {
            if (title != null) 'title': title,
            if (spaceId != null) 'spaceId': spaceId,
          },
        ),
      );

  Future<void> renameSession(String id, String title) =>
      _api.put<void>('ai/sessions/$id/title', body: {'title': title});

  Future<void> deleteSession(String id) =>
      _api.delete<void>('ai/sessions/$id');

  Future<void> compactSession(String id) =>
      _api.post<void>('ai/sessions/$id/compact', body: const {});

  /// Streams the assistant's reply.
  ///
  /// The reply comes back as Server-Sent Events, which cannot be consumed with
  /// an `EventSource`-style client here: the endpoint is a POST and needs the
  /// bearer token, and `EventSource` is GET-only with no way to set headers.
  /// So the raw response body is read and framed by hand.
  Stream<ChatStreamEvent> streamMessage({
    required String sessionId,
    required String message,
    CancelToken? cancelToken,
  }) async* {
    late final Response<ResponseBody> response;
    try {
      response = await _api.raw.post<ResponseBody>(
        'ai/sessions/$sessionId/messages/stream',
        data: {'message': message},
        cancelToken: cancelToken,
        options: Options(
          responseType: ResponseType.stream,
          headers: {'Accept': 'text/event-stream'},
          // A model can think for a long time between tokens, and each token
          // resets nothing — so no receive timeout, or a slow reply is killed
          // mid-sentence.
          receiveTimeout: Duration.zero,
        ),
      );
    } on DioException catch (e) {
      final api = ApiException.from(e);
      yield ChatStreamError(
        code: api.isUnauthorized ? 'unauthorized' : 'http_error',
        message: api.message,
      );
      return;
    }

    final body = response.data;
    if (body == null) {
      yield const ChatStreamError(
        code: 'http_error',
        message: 'The server sent an empty response.',
      );
      return;
    }

    // SSE frames are separated by a blank line. Chunks split anywhere, so a
    // partial frame is buffered until its terminator arrives.
    var buffer = '';
    final decoder = const Utf8Decoder(allowMalformed: true);

    try {
      await for (final Uint8List chunk in body.stream) {
        buffer += decoder.convert(chunk);

        var separator = buffer.indexOf('\n\n');
        while (separator != -1) {
          final frame = buffer.substring(0, separator);
          buffer = buffer.substring(separator + 2);

          final event = _parseFrame(frame);
          if (event != null) {
            yield event;
            if (event is ChatStreamDone) return;
          }
          separator = buffer.indexOf('\n\n');
        }
      }
    } on DioException catch (e) {
      if (CancelToken.isCancel(e)) return;
      yield ChatStreamError(
        code: 'stream_error',
        message: ApiException.from(e).message,
      );
    }
  }

  /// Parses one `event: <name>` / `data: <json>` frame.
  static ChatStreamEvent? _parseFrame(String frame) {
    String? name;
    final data = StringBuffer();

    for (final line in frame.split('\n')) {
      if (line.startsWith('event:')) {
        name = line.substring(6).trim();
      } else if (line.startsWith('data:')) {
        // Multi-line data fields concatenate, per the SSE spec.
        if (data.isNotEmpty) data.write('\n');
        data.write(line.substring(5).trim());
      }
    }

    final payload = data.toString();
    if (name == null || payload.isEmpty) return null;

    switch (name) {
      case 'done':
        return const ChatStreamDone();

      case 'message':
        try {
          return ChatDelta.fromJson(
            jsonDecode(payload) as Map<String, dynamic>,
          );
        } catch (_) {
          // A malformed frame should not kill the stream; skip it.
          return null;
        }

      case 'error':
        try {
          final j = jsonDecode(payload) as Map<String, dynamic>;
          return ChatStreamError(
            code: (j['code'] ?? 'ai_error').toString(),
            message: (j['message'] ?? 'The assistant hit an error.').toString(),
          );
        } catch (_) {
          return const ChatStreamError(
            code: 'ai_error',
            message: 'The assistant hit an error.',
          );
        }

      default:
        return null;
    }
  }
}

final chatRepositoryProvider = Provider<ChatRepository>(
  (ref) => ChatRepository(ref.watch(apiClientProvider)),
);
