import 'dart:convert';
import 'dart:typed_data';

import '../../tasks/data/task_models.dart' show parseApiInstant;

/// An image staged for sending, before the round trip.
///
/// `image_picker` always produces real JPEG bytes on iOS regardless of the
/// source format — HEIC included, since `UIImage`/`UIImageJPEGRepresentation`
/// re-encodes rather than passing the container through — so `fileName`
/// always ends up `.jpg` here even for a photo picked straight off the
/// camera roll. That matters because the server's `AiAttachmentHelper` only
/// recognises .png/.jpg/.jpeg/.gif/.webp and throws on anything else.
class PendingAttachment {
  const PendingAttachment({
    required this.fileName,
    required this.bytes,
  });

  final String fileName;
  final Uint8List bytes;

  /// What `SendAiChatMessageRequest.Attachments` expects: base64 content
  /// alongside the file name the server uses to pick a MIME type.
  Map<String, dynamic> toJson() => {
    'fileName': fileName,
    'content': base64Encode(bytes),
  };
}

class ChatSession {
  const ChatSession({
    required this.id,
    this.title,
    this.lastMessageAt,
    this.createdOn,
    this.provider,
    this.model,
    this.spaceId,
  });

  final String id;
  final String? title;
  final DateTime? lastMessageAt;
  final DateTime? createdOn;
  final String? provider;
  final String? model;
  final String? spaceId;

  /// The server leaves `title` null until a conversation has content, so the
  /// list needs a stand-in rather than a blank row.
  String get displayTitle {
    final t = title?.trim();
    return (t == null || t.isEmpty) ? 'New chat' : t;
  }

  factory ChatSession.fromJson(Map<String, dynamic> j) => ChatSession(
    id: (j['id'] ?? '').toString(),
    title: j['title'] as String?,
    lastMessageAt: parseApiInstant(j['lastMessageAt']),
    createdOn: parseApiInstant(j['createdOn']),
    provider: j['provider'] as String?,
    model: j['model'] as String?,
    spaceId: j['spaceId']?.toString(),
  );
}

class ChatMessage {
  const ChatMessage({
    required this.id,
    required this.role,
    required this.sequence,
    required this.isError,
    this.content,
    this.createdOn,
    this.attachments = const [],
    this.localImages = const [],
    this.finishReason,
    this.inputTokens,
    this.outputTokens,
  });

  final String id;
  final String role;
  final String? content;
  final int sequence;
  final bool isError;
  final DateTime? createdOn;
  final List<String> attachments;

  /// Raw bytes for a locally-echoed message's images, shown until the reload
  /// after `done` replaces this message with the server's persisted version.
  /// The server never returns image bytes in the message list — only
  /// filenames — so this exists purely for that in-between moment.
  final List<PendingAttachment> localImages;
  final String? finishReason;
  final int? inputTokens;
  final int? outputTokens;

  bool get isUser => role.toLowerCase() == 'user';
  bool get isAssistant => role.toLowerCase() == 'assistant';

  /// System and tool messages are part of the transcript but not the
  /// conversation a person had; the UI hides them.
  bool get isVisible => isUser || isAssistant;

  factory ChatMessage.fromJson(Map<String, dynamic> j) => ChatMessage(
    id: (j['id'] ?? '').toString(),
    role: (j['role'] ?? '').toString(),
    content: j['content'] as String?,
    sequence: (j['sequence'] as num?)?.toInt() ?? 0,
    isError: j['isError'] == true,
    createdOn: parseApiInstant(j['createdOn']),
    attachments: ((j['attachments'] as List?) ?? const [])
        .map((e) => e.toString())
        .toList(growable: false),
    finishReason: j['finishReason'] as String?,
    inputTokens: (j['inputTokens'] as num?)?.toInt(),
    outputTokens: (j['outputTokens'] as num?)?.toInt(),
  );

  /// A local, not-yet-persisted message — the user's line echoed immediately,
  /// and the assistant's reply as it streams in.
  factory ChatMessage.local({
    required String role,
    required String content,
    required int sequence,
    bool isError = false,
    List<PendingAttachment> localImages = const [],
  }) => ChatMessage(
    id: 'local-$role-$sequence',
    role: role,
    content: content,
    sequence: sequence,
    isError: isError,
    createdOn: DateTime.now(),
    localImages: localImages,
  );

  ChatMessage copyWith({String? content, bool? isError}) => ChatMessage(
    id: id,
    role: role,
    content: content ?? this.content,
    sequence: sequence,
    isError: isError ?? this.isError,
    createdOn: createdOn,
    attachments: attachments,
    localImages: localImages,
    finishReason: finishReason,
    inputTokens: inputTokens,
    outputTokens: outputTokens,
  );
}

class ChatSessionDetail {
  const ChatSessionDetail({required this.session, required this.messages});

  final ChatSession session;
  final List<ChatMessage> messages;

  factory ChatSessionDetail.fromJson(Map<String, dynamic> j) =>
      ChatSessionDetail(
        session: ChatSession.fromJson(j),
        messages:
            ((j['messages'] as List?) ?? const [])
                .whereType<Map>()
                .map((e) => ChatMessage.fromJson(e.cast<String, dynamic>()))
                .toList()
              ..sort((a, b) => a.sequence.compareTo(b.sequence)),
      );
}

/// Mirrors `AiChatStreamUpdate`. Phase 0 is compaction — the server summarising
/// an over-long history before it can answer — which is worth showing, because
/// it can take a while and looks like a hang otherwise.
enum StreamPhase {
  compacting(0),
  generating(1),
  completed(2);

  const StreamPhase(this.wire);
  final int wire;

  static StreamPhase parse(Object? v) {
    final i = (v as num?)?.toInt() ?? 1;
    return StreamPhase.values.firstWhere(
      (p) => p.wire == i,
      orElse: () => StreamPhase.generating,
    );
  }
}

/// One frame off the SSE stream.
sealed class ChatStreamEvent {
  const ChatStreamEvent();
}

class ChatDelta extends ChatStreamEvent {
  const ChatDelta({
    required this.phase,
    this.textDelta,
    this.isFinal = false,
    this.finishReason,
  });

  final StreamPhase phase;
  final String? textDelta;
  final bool isFinal;
  final String? finishReason;

  factory ChatDelta.fromJson(Map<String, dynamic> j) => ChatDelta(
    phase: StreamPhase.parse(j['phase']),
    textDelta: j['textDelta'] as String?,
    isFinal: j['isFinal'] == true,
    finishReason: j['finishReason'] as String?,
  );
}

class ChatStreamError extends ChatStreamEvent {
  const ChatStreamError({required this.code, required this.message});

  final String code;
  final String message;

  /// The server has a specific code for "no AI provider configured", which
  /// needs a different answer than a transient failure.
  bool get isNotConfigured => code == 'ai_not_configured';
  bool get isCredentialRejected => code == 'ai_credential_rejected';
}

class ChatStreamDone extends ChatStreamEvent {
  const ChatStreamDone();
}
