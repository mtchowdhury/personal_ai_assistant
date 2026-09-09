import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/app_states.dart';
import '../data/chat_controller.dart';
import '../data/chat_models.dart';
import '../data/chat_repository.dart';

/// One conversation, with the reply streaming in.
class ConversationScreen extends ConsumerStatefulWidget {
  const ConversationScreen({super.key, required this.sessionId});

  final String sessionId;

  @override
  ConsumerState<ConversationScreen> createState() => _ConversationScreenState();
}

class _ConversationScreenState extends ConsumerState<ConversationScreen> {
  final _input = TextEditingController();
  final _scroll = ScrollController();

  @override
  void dispose() {
    _input.dispose();
    _scroll.dispose();
    super.dispose();
  }

  /// Keeps the newest text in view as it streams. Jumps rather than animates
  /// during a stream — an animation per token would never settle.
  void _scrollToEnd({bool animate = false}) {
    if (!_scroll.hasClients) return;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!_scroll.hasClients) return;
      final target = _scroll.position.maxScrollExtent;
      if (animate) {
        _scroll.animateTo(
          target,
          duration: const Duration(milliseconds: 220),
          curve: Curves.easeOut,
        );
      } else {
        _scroll.jumpTo(target);
      }
    });
  }

  Future<void> _send() async {
    final text = _input.text.trim();
    if (text.isEmpty) return;
    _input.clear();
    setState(() {});
    _scrollToEnd(animate: true);
    await ref.read(chatProvider(widget.sessionId).notifier).send(text);
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(chatProvider(widget.sessionId));

    // Follow the stream as new text lands.
    ref.listen(chatProvider(widget.sessionId), (_, next) {
      if (next.value?.streaming ?? false) _scrollToEnd();
    });

    final state = async.value;
    final streaming = state?.streaming ?? false;

    return Scaffold(
      appBar: AppBar(
        title: Text(
          state?.session?.displayTitle ?? 'Chat',
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w700),
        ),
        actions: [
          PopupMenuButton<String>(
            onSelected: (v) => _menu(v),
            itemBuilder: (_) => const [
              PopupMenuItem(
                value: 'rename',
                child: ListTile(
                  dense: true,
                  contentPadding: EdgeInsets.zero,
                  leading: Icon(Icons.edit_outlined),
                  title: Text('Rename'),
                ),
              ),
              PopupMenuItem(
                value: 'compact',
                child: ListTile(
                  dense: true,
                  contentPadding: EdgeInsets.zero,
                  leading: Icon(Icons.compress_rounded),
                  title: Text('Compact history'),
                ),
              ),
            ],
          ),
        ],
      ),
      body: Column(
        children: [
          Expanded(
            child: async.when(
              loading: () => const LoadingState(),
              error: (e, _) => ErrorState(
                error: e,
                onRetry: () =>
                    ref.invalidate(chatProvider(widget.sessionId)),
              ),
              data: (s) => s.messages.isEmpty
                  ? const EmptyState(
                      icon: Icons.auto_awesome_outlined,
                      title: 'Say something',
                      message:
                          'Try "log €12 lunch at Pret" or '
                          '"what did I spend on groceries this month?"',
                    )
                  : ListView.builder(
                      controller: _scroll,
                      padding: const EdgeInsets.fromLTRB(
                        Gap.lg,
                        Gap.lg,
                        Gap.lg,
                        Gap.lg,
                      ),
                      itemCount: s.messages.length,
                      itemBuilder: (context, i) {
                        final m = s.messages[i];
                        final isLast = i == s.messages.length - 1;
                        return _Bubble(
                          message: m,
                          // Only the final assistant bubble can be mid-stream.
                          streaming: streaming && isLast && m.isAssistant,
                          compacting: s.isCompacting && isLast,
                        );
                      },
                    ),
            ),
          ),

          if (state?.error case final e?) _ErrorBar(message: e),

          _Composer(
            controller: _input,
            streaming: streaming,
            onSend: _send,
            onStop: () =>
                ref.read(chatProvider(widget.sessionId).notifier).stop(),
            onChanged: () => setState(() {}),
          ),
        ],
      ),
    );
  }

  Future<void> _menu(String action) async {
    switch (action) {
      case 'rename':
        await _rename();
      case 'compact':
        try {
          await ref.read(chatProvider(widget.sessionId).notifier).compact();
          if (mounted) showAppSnack(context, 'History compacted');
        } on ApiException catch (e) {
          if (mounted) showAppSnack(context, e.message, error: true);
        }
    }
  }

  Future<void> _rename() async {
    final current =
        ref.read(chatProvider(widget.sessionId)).value?.session?.title ?? '';
    final controller = TextEditingController(text: current);

    final name = await showDialog<String>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Rename chat'),
        content: TextField(
          controller: controller,
          autofocus: true,
          textCapitalization: TextCapitalization.sentences,
          onSubmitted: (v) => Navigator.of(dialogContext).pop(v),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(controller.text),
            child: const Text('Save'),
          ),
        ],
      ),
    );
    controller.dispose();

    if (name == null || name.trim().isEmpty) return;
    try {
      await ref
          .read(chatRepositoryProvider)
          .renameSession(widget.sessionId, name.trim());
      ref.invalidate(chatProvider(widget.sessionId));
      ref.invalidate(chatSessionsProvider);
    } on ApiException catch (e) {
      if (mounted) showAppSnack(context, e.message, error: true);
    }
  }
}

class _Bubble extends StatelessWidget {
  const _Bubble({
    required this.message,
    this.streaming = false,
    this.compacting = false,
  });

  final ChatMessage message;
  final bool streaming;
  final bool compacting;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final isUser = message.isUser;
    final text = message.content ?? '';

    // Compaction happens before any text exists, so it gets its own state
    // rather than an empty bubble that looks like a hang.
    if (compacting && text.isEmpty) {
      return const Padding(
        padding: EdgeInsets.only(bottom: Gap.md),
        child: Row(
          children: [
            SizedBox(
              width: 14,
              height: 14,
              child: CircularProgressIndicator(strokeWidth: 2),
            ),
            SizedBox(width: Gap.sm),
            Text(
              'Summarising the conversation so far…',
              style: TextStyle(fontSize: 13, fontStyle: FontStyle.italic),
            ),
          ],
        ),
      );
    }

    return Padding(
      padding: const EdgeInsets.only(bottom: Gap.md),
      child: Row(
        mainAxisAlignment: isUser
            ? MainAxisAlignment.end
            : MainAxisAlignment.start,
        children: [
          Flexible(
            child: Container(
              constraints: BoxConstraints(
                // Never full width: the ragged edge is what makes the two
                // sides of the conversation distinguishable at a glance.
                maxWidth: MediaQuery.sizeOf(context).width * 0.82,
              ),
              padding: const EdgeInsets.symmetric(
                horizontal: Gap.md,
                vertical: Gap.md - 2,
              ),
              decoration: BoxDecoration(
                color: message.isError
                    ? AppColors.red.withValues(alpha: 0.1)
                    : isUser
                    ? AppColors.accent
                    : scheme.surfaceContainerHighest.withValues(alpha: 0.65),
                borderRadius: BorderRadius.only(
                  topLeft: const Radius.circular(Radii.card),
                  topRight: const Radius.circular(Radii.card),
                  bottomLeft: Radius.circular(isUser ? Radii.card : 4),
                  bottomRight: Radius.circular(isUser ? 4 : Radii.card),
                ),
              ),
              child: text.isEmpty && streaming
                  ? const _Typing()
                  : SelectableText(
                      text,
                      style: TextStyle(
                        fontSize: 15,
                        height: 1.42,
                        color: message.isError
                            ? AppColors.red
                            : isUser
                            ? Colors.white
                            : scheme.onSurface,
                      ),
                    ),
            ),
          ),
        ],
      ),
    );
  }
}

/// Three pulsing dots while the first token is still on its way.
class _Typing extends StatefulWidget {
  const _Typing();

  @override
  State<_Typing> createState() => _TypingState();
}

class _TypingState extends State<_Typing> with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 1100),
  )..repeat();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final color = Theme.of(context).colorScheme.onSurfaceVariant;

    return AnimatedBuilder(
      animation: _controller,
      builder: (context, _) => Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          for (var i = 0; i < 3; i++) ...[
            if (i > 0) const SizedBox(width: 4),
            Opacity(
              // Staggered thirds, so the dots chase rather than blink together.
              opacity: 0.3 + 0.7 * _pulse((_controller.value + i / 3) % 1),
              child: Container(
                width: 6,
                height: 6,
                decoration: BoxDecoration(color: color, shape: BoxShape.circle),
              ),
            ),
          ],
        ],
      ),
    );
  }

  /// A smooth up-and-back over the cycle.
  static double _pulse(double t) => t < 0.5 ? t * 2 : (1 - t) * 2;
}

class _ErrorBar extends StatelessWidget {
  const _ErrorBar({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) => Container(
    width: double.infinity,
    color: AppColors.red.withValues(alpha: 0.1),
    padding: const EdgeInsets.symmetric(
      horizontal: Gap.lg,
      vertical: Gap.sm,
    ),
    child: Row(
      children: [
        const Icon(Icons.error_outline_rounded, size: 15, color: AppColors.red),
        const SizedBox(width: Gap.sm),
        Expanded(
          child: Text(
            message,
            style: const TextStyle(fontSize: 12.5, color: AppColors.red),
          ),
        ),
      ],
    ),
  );
}

class _Composer extends StatelessWidget {
  const _Composer({
    required this.controller,
    required this.streaming,
    required this.onSend,
    required this.onStop,
    required this.onChanged,
  });

  final TextEditingController controller;
  final bool streaming;
  final VoidCallback onSend;
  final VoidCallback onStop;
  final VoidCallback onChanged;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final canSend = controller.text.trim().isNotEmpty && !streaming;

    return SafeArea(
      top: false,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(Gap.md, Gap.sm, Gap.md, Gap.sm),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Expanded(
              child: TextField(
                controller: controller,
                onChanged: (_) => onChanged(),
                textCapitalization: TextCapitalization.sentences,
                // Grows with the message but stops before it eats the screen.
                minLines: 1,
                maxLines: 5,
                keyboardType: TextInputType.multiline,
                decoration: InputDecoration(
                  hintText: 'Message cmdnext',
                  isDense: true,
                  contentPadding: const EdgeInsets.symmetric(
                    horizontal: Gap.md,
                    vertical: Gap.md - 2,
                  ),
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(Radii.sheet),
                    borderSide: BorderSide(color: scheme.outlineVariant),
                  ),
                  enabledBorder: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(Radii.sheet),
                    borderSide: BorderSide(color: scheme.outlineVariant),
                  ),
                  focusedBorder: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(Radii.sheet),
                    borderSide: const BorderSide(
                      color: AppColors.accent,
                      width: 1.6,
                    ),
                  ),
                ),
              ),
            ),
            const SizedBox(width: Gap.sm),
            // Send becomes Stop mid-stream — the same spot, so interrupting a
            // long answer does not mean hunting for a second control.
            SizedBox(
              width: 44,
              height: 44,
              child: Material(
                color: streaming
                    ? scheme.surfaceContainerHighest
                    : canSend
                    ? AppColors.accent
                    : scheme.surfaceContainerHighest,
                shape: const CircleBorder(),
                child: InkWell(
                  customBorder: const CircleBorder(),
                  onTap: streaming
                      ? () {
                          HapticFeedback.lightImpact();
                          onStop();
                        }
                      : canSend
                      ? () {
                          HapticFeedback.lightImpact();
                          onSend();
                        }
                      : null,
                  child: Icon(
                    streaming ? Icons.stop_rounded : Icons.arrow_upward_rounded,
                    size: 20,
                    color: streaming
                        ? scheme.onSurface
                        : canSend
                        ? Colors.white
                        : scheme.onSurfaceVariant,
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
