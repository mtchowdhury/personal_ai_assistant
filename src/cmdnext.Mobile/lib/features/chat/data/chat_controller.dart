import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'chat_models.dart';
import 'chat_repository.dart';

final chatSessionsProvider = FutureProvider<List<ChatSession>>(
  (ref) => ref.watch(chatRepositoryProvider).sessions(),
);

/// What one conversation looks like right now, including the in-flight reply.
class ChatState {
  const ChatState({
    this.session,
    this.messages = const [],
    this.streaming = false,
    this.phase = StreamPhase.generating,
    this.error,
  });

  final ChatSession? session;
  final List<ChatMessage> messages;
  final bool streaming;
  final StreamPhase phase;
  final String? error;

  /// True while the server is summarising an over-long history before it can
  /// answer. Worth surfacing — it can take seconds and looks like a hang.
  bool get isCompacting => streaming && phase == StreamPhase.compacting;

  ChatState copyWith({
    ChatSession? session,
    List<ChatMessage>? messages,
    bool? streaming,
    StreamPhase? phase,
    String? error,
    bool clearError = false,
  }) => ChatState(
    session: session ?? this.session,
    messages: messages ?? this.messages,
    streaming: streaming ?? this.streaming,
    phase: phase ?? this.phase,
    error: clearError ? null : (error ?? this.error),
  );
}

/// Drives one conversation. Family-keyed by session id so switching sessions
/// does not bleed messages between them.
class ChatController extends AsyncNotifier<ChatState> {
  ChatController(this.sessionId);

  final String sessionId;

  CancelToken? _cancel;

  @override
  Future<ChatState> build() async {
    // Cancelling on dispose stops a stream that nobody is watching any more.
    ref.onDispose(() => _cancel?.cancel());

    final detail = await ref.watch(chatRepositoryProvider).session(sessionId);
    return ChatState(
      session: detail.session,
      messages: detail.messages.where((m) => m.isVisible).toList(),
    );
  }

  /// Sends a message and streams the reply.
  ///
  /// The user's line and an empty assistant bubble are appended immediately so
  /// the conversation moves the instant Send is tapped; the bubble then fills
  /// as deltas arrive. A message with no text but at least one image is
  /// allowed — "what's this?" plus a photo needs no words at all.
  Future<void> send(
    String text, {
    List<PendingAttachment> attachments = const [],
  }) async {
    final trimmed = text.trim();
    final current = state.value;
    if ((trimmed.isEmpty && attachments.isEmpty) ||
        current == null ||
        current.streaming) {
      return;
    }

    final nextSequence = current.messages.isEmpty
        ? 0
        : current.messages.last.sequence + 1;

    final userMessage = ChatMessage.local(
      role: 'user',
      content: trimmed,
      sequence: nextSequence,
      localImages: attachments,
    );
    var assistant = ChatMessage.local(
      role: 'assistant',
      content: '',
      sequence: nextSequence + 1,
    );

    state = AsyncData(
      current.copyWith(
        messages: [...current.messages, userMessage, assistant],
        streaming: true,
        phase: StreamPhase.generating,
        clearError: true,
      ),
    );

    // Accumulated separately: rebuilding the whole list per token would mean
    // re-allocating every message on every frame.
    final buffer = StringBuffer();
    _cancel = CancelToken();

    void publish({bool streaming = true, String? error, bool isError = false}) {
      final snapshot = state.value;
      if (snapshot == null) return;
      assistant = assistant.copyWith(
        content: buffer.toString(),
        isError: isError,
      );
      state = AsyncData(
        snapshot.copyWith(
          messages: [
            for (final m in snapshot.messages)
              if (m.id == assistant.id) assistant else m,
          ],
          streaming: streaming,
          error: error,
        ),
      );
    }

    try {
      final stream = ref
          .read(chatRepositoryProvider)
          .streamMessage(
            sessionId: sessionId,
            message: trimmed,
            attachments: attachments,
            cancelToken: _cancel,
          );

      await for (final event in stream) {
        switch (event) {
          case ChatDelta(:final phase, :final textDelta):
            final snapshot = state.value;
            if (snapshot != null && snapshot.phase != phase) {
              state = AsyncData(snapshot.copyWith(phase: phase));
            }
            if (textDelta != null && textDelta.isNotEmpty) {
              buffer.write(textDelta);
              publish();
            }

          case ChatStreamError(:final message):
            if (buffer.isEmpty) buffer.write(message);
            publish(streaming: false, error: message, isError: true);
            return;

          case ChatStreamDone():
            publish(streaming: false);
            // Reload so the messages carry their real server ids, token
            // counts, and the title the server may have just generated.
            await _reload();
            return;
        }
      }

      // The stream ended without a `done` frame — treat it as finished
      // rather than leaving the UI spinning forever.
      publish(streaming: false);
      await _reload();
    } finally {
      _cancel = null;
    }
  }

  /// Stops an in-flight reply, keeping whatever text already arrived.
  void stop() {
    _cancel?.cancel();
    _cancel = null;
    final snapshot = state.value;
    if (snapshot != null) {
      state = AsyncData(snapshot.copyWith(streaming: false));
    }
  }

  Future<void> _reload() async {
    try {
      final detail = await ref.read(chatRepositoryProvider).session(sessionId);
      state = AsyncData(
        ChatState(
          session: detail.session,
          messages: detail.messages.where((m) => m.isVisible).toList(),
        ),
      );
      // The session list shows titles and timestamps that just changed.
      ref.invalidate(chatSessionsProvider);
    } catch (_) {
      // Keeping the streamed text is better than blanking the conversation
      // because a follow-up fetch failed.
    }
  }

  /// Summarises the history server-side when a conversation gets long.
  Future<void> compact() async {
    await ref.read(chatRepositoryProvider).compactSession(sessionId);
    await _reload();
  }
}

final chatProvider = AsyncNotifierProvider.autoDispose
    .family<ChatController, ChatState, String>(ChatController.new);
