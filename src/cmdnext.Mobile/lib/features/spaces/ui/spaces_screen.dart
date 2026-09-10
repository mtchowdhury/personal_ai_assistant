import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/app_states.dart';
import '../data/space_models.dart';
import '../data/spaces_controller.dart';
import '../data/spaces_repository.dart';

/// The Spaces tab: one card per space, a way to search across all of them,
/// and a way to create a new one from a template.
class SpacesScreen extends ConsumerWidget {
  const SpacesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(spacesListProvider);

    return Scaffold(
      body: RefreshIndicator(
        onRefresh: () async => ref.invalidate(spacesListProvider),
        child: async.when(
          loading: () => const LoadingState(),
          error: (e, _) => CustomScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            slivers: [
              SliverFillRemaining(
                hasScrollBody: false,
                child: ErrorState(
                  error: e,
                  onRetry: () => ref.invalidate(spacesListProvider),
                ),
              ),
            ],
          ),
          data: (spaces) => CustomScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            slivers: [
              SliverAppBar.large(
                title: const Text('Spaces'),
                actions: [
                  IconButton(
                    tooltip: 'Search',
                    icon: const Icon(Icons.search_rounded),
                    onPressed: () => context.push('/spaces/search'),
                  ),
                ],
              ),
              if (spaces.isEmpty)
                SliverFillRemaining(
                  hasScrollBody: false,
                  child: EmptyState(
                    icon: Icons.layers_outlined,
                    title: 'No spaces yet',
                    message:
                        'A space holds notes, logs, or anything you '
                        'want to keep organised your own way.',
                    action: FilledButton.icon(
                      onPressed: () => _createSpace(context, ref),
                      icon: const Icon(Icons.add_rounded, size: 20),
                      label: const Text('Create a space'),
                    ),
                  ),
                )
              else
                SliverPadding(
                  padding: const EdgeInsets.fromLTRB(Gap.lg, 0, Gap.lg, 96),
                  sliver: SliverList.separated(
                    itemCount: spaces.length,
                    separatorBuilder: (_, _) => const SizedBox(height: Gap.md),
                    itemBuilder: (context, i) => _SpaceCard(space: spaces[i]),
                  ),
                ),
            ],
          ),
        ),
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () => _createSpace(context, ref),
        tooltip: 'New space',
        child: const Icon(Icons.add_rounded),
      ),
    );
  }

  Future<void> _createSpace(BuildContext context, WidgetRef ref) async {
    final templates = await ref.read(spaceTemplatesProvider.future);
    if (!context.mounted) return;

    final picked = await showModalBottomSheet<SpaceTemplate>(
      context: context,
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(Gap.lg, Gap.lg, Gap.lg, Gap.sm),
              child: Text(
                'New space',
                style: Theme.of(sheetContext).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
            for (final t in templates)
              ListTile(
                leading: Icon(t.icon, color: AppColors.accent),
                title: Text(t.label),
                subtitle: Text(
                  t.description,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
                onTap: () => Navigator.of(sheetContext).pop(t),
              ),
          ],
        ),
      ),
    );
    if (picked == null || !context.mounted) return;

    final name = await _promptName(context, picked.label);
    if (name == null || name.trim().isEmpty) return;

    try {
      final space = await ref.read(spacesRepositoryProvider).createSpace(
        name: name.trim(),
        kind: picked.kind,
      );
      ref.invalidate(spacesListProvider);
      if (context.mounted) context.push('/spaces/${space.id}');
    } on ApiException catch (e) {
      if (context.mounted) showAppSnack(context, e.message, error: true);
    }
  }

  Future<String?> _promptName(BuildContext context, String templateLabel) {
    final controller = TextEditingController();
    return showDialog<String>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text('Name your $templateLabel'),
        content: TextField(
          controller: controller,
          autofocus: true,
          textCapitalization: TextCapitalization.words,
          onSubmitted: (v) => Navigator.of(dialogContext).pop(v),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(controller.text),
            child: const Text('Create'),
          ),
        ],
      ),
    );
  }
}

class _SpaceCard extends StatelessWidget {
  const _SpaceCard({required this.space});

  final Space space;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return Card(
      child: InkWell(
        onTap: () => context.push('/spaces/${space.id}'),
        borderRadius: BorderRadius.circular(Radii.card),
        child: Padding(
          padding: const EdgeInsets.all(Gap.lg),
          child: Row(
            children: [
              Container(
                width: 44,
                height: 44,
                decoration: BoxDecoration(
                  color: AppColors.accent.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(12),
                ),
                alignment: Alignment.center,
                child: Icon(space.icon, color: AppColors.accent, size: 21),
              ),
              const SizedBox(width: Gap.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      space.name,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    if (space.description case final d? when d.trim().isNotEmpty) ...[
                      const SizedBox(height: 2),
                      Text(
                        d,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(fontSize: 13, color: scheme.onSurfaceVariant),
                      ),
                    ],
                  ],
                ),
              ),
              Icon(Icons.chevron_right_rounded, size: 20, color: scheme.onSurfaceVariant),
            ],
          ),
        ),
      ),
    );
  }
}
