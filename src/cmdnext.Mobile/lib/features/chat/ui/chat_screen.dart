import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/app_states.dart';
import '../data/chat_controller.dart';
import '../data/chat_models.dart';
import '../data/chat_repository.dart';

/// The list of conversations.
///
/// The web client keeps sessions in a side rail next to the transcript. On a
/// phone there is no room for both, so this is a list that pushes into a
/// conversation — and the primary action is starting a new one, since that is
/// what someone opening the tab usually wants.
class ChatScreen extends ConsumerWidget {
  const ChatScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(chatSessionsProvider);

    return Scaffold(
      body: RefreshIndicator(
        onRefresh: () async => ref.invalidate(chatSessionsProvider),
        child: async.when(
          loading: () => const LoadingState(),
          error: (e, _) => CustomScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            slivers: [
              SliverFillRemaining(
                hasScrollBody: false,
                child: ErrorState(
                  error: e,
                  onRetry: () => ref.invalidate(chatSessionsProvider),
                ),
              ),
            ],
          ),
          data: (sessions) => CustomScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            slivers: [
              const SliverAppBar.large(title: Text('Ask')),
              if (sessions.isEmpty)
                SliverFillRemaining(
                  hasScrollBody: false,
                  child: EmptyState(
                    icon: Icons.auto_awesome_outlined,
                    title: 'Ask cmdnext anything',
                    message:
                        'It can log expenses, add tasks, and search your '
                        'spaces — just say what you need.',
                    action: FilledButton.icon(
                      onPressed: () => _newChat(context, ref),
                      icon: const Icon(Icons.add_rounded, size: 20),
                      label: const Text('Start a chat'),
                    ),
                  ),
                )
              else
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(
                      Gap.lg,
                      0,
                      Gap.lg,
                      96,
                    ),
                    child: Card(
                      child: Column(
                        children: [
                          for (final (i, s) in sessions.indexed) ...[
                            if (i > 0) const Divider(indent: 60),
                            _SessionRow(session: s),
                          ],
                        ],
                      ),
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => _newChat(context, ref),
        icon: const Icon(Icons.add_rounded),
        label: const Text('New chat'),
      ),
    );
  }

  Future<void> _newChat(BuildContext context, WidgetRef ref) async {
    try {
      final session = await ref.read(chatRepositoryProvider).createSession();
      ref.invalidate(chatSessionsProvider);
      if (context.mounted) context.push('/chat/${session.id}');
    } on ApiException catch (e) {
      if (context.mounted) showAppSnack(context, e.message, error: true);
    }
  }
}

class _SessionRow extends ConsumerWidget {
  const _SessionRow({required this.session});

  final ChatSession session;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final scheme = Theme.of(context).colorScheme;

    return Dismissible(
      key: ValueKey(session.id),
      direction: DismissDirection.endToStart,
      background: Container(
        color: AppColors.red,
        alignment: Alignment.centerRight,
        padding: const EdgeInsets.symmetric(horizontal: Gap.xl),
        child: const Icon(Icons.delete_rounded, color: Colors.white, size: 20),
      ),
      confirmDismiss: (_) async {
        final ok = await showDialog<bool>(
          context: context,
          builder: (dialogContext) => AlertDialog(
            title: const Text('Delete chat?'),
            content: Text(session.displayTitle),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(dialogContext).pop(false),
                child: const Text('Cancel'),
              ),
              TextButton(
                onPressed: () => Navigator.of(dialogContext).pop(true),
                style: TextButton.styleFrom(foregroundColor: AppColors.red),
                child: const Text('Delete'),
              ),
            ],
          ),
        );
        return ok ?? false;
      },
      onDismissed: (_) async {
        try {
          await ref.read(chatRepositoryProvider).deleteSession(session.id);
        } on ApiException catch (e) {
          if (context.mounted) showAppSnack(context, e.message, error: true);
        }
        ref.invalidate(chatSessionsProvider);
      },
      child: InkWell(
        onTap: () => context.push('/chat/${session.id}'),
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: Gap.lg,
            vertical: Gap.md,
          ),
          child: Row(
            children: [
              Container(
                width: 34,
                height: 34,
                decoration: BoxDecoration(
                  color: AppColors.accent.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(10),
                ),
                alignment: Alignment.center,
                child: const Icon(
                  Icons.auto_awesome_rounded,
                  size: 16,
                  color: AppColors.accent,
                ),
              ),
              const SizedBox(width: Gap.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      session.displayTitle,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    if (session.lastMessageAt case final t?) ...[
                      const SizedBox(height: 2),
                      Text(
                        _relative(t),
                        style: TextStyle(
                          fontSize: 12,
                          color: scheme.onSurfaceVariant,
                        ),
                      ),
                    ],
                  ],
                ),
              ),
              Icon(
                Icons.chevron_right_rounded,
                size: 19,
                color: scheme.onSurfaceVariant,
              ),
            ],
          ),
        ),
      ),
    );
  }

  /// Relative time, because "2h ago" tells you more at a glance than a
  /// timestamp when scanning a list of conversations.
  static String _relative(DateTime t) {
    final diff = DateTime.now().difference(t);
    if (diff.inMinutes < 1) return 'Just now';
    if (diff.inMinutes < 60) return '${diff.inMinutes}m ago';
    if (diff.inHours < 24) return '${diff.inHours}h ago';
    if (diff.inDays == 1) return 'Yesterday';
    if (diff.inDays < 7) return '${diff.inDays} days ago';
    return DateFormat('d MMM').format(t);
  }
}
