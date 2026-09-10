import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/app_states.dart';
import '../data/space_models.dart';
import '../data/spaces_controller.dart';
import 'create_entry_sheet.dart';

/// A space, opened to its timeline.
///
/// The web client leads with the node tree — a file-browser feel that fits a
/// mouse. On a phone the more common motion is "what's recent, and let me add
/// to it", so this opens straight to a reverse-chronological feed; nodes
/// become a horizontal filter strip you slide into rather than a tree you
/// navigate down through.
class SpaceDetailScreen extends ConsumerWidget {
  const SpaceDetailScreen({super.key, required this.spaceId});

  final String spaceId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final spaceAsync = ref.watch(spaceProvider(spaceId));

    return Scaffold(
      body: spaceAsync.when(
        loading: () => const LoadingState(),
        error: (e, _) => ErrorState(
          error: e,
          onRetry: () => ref.invalidate(spaceProvider(spaceId)),
        ),
        data: (space) => _Body(space: space),
      ),
      floatingActionButton: spaceAsync.value == null
          ? null
          : FloatingActionButton(
              onPressed: () async {
                final nodeId = ref.read(nodeFilterProvider(spaceId));
                await showCreateEntrySheet(
                  context,
                  space: spaceAsync.value!,
                  nodeId: nodeId,
                );
              },
              tooltip: 'New entry',
              child: const Icon(Icons.add_rounded),
            ),
    );
  }
}

class _Body extends ConsumerWidget {
  const _Body({required this.space});

  final Space space;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final timeline = ref.watch(spaceTimelineProvider(space.id));
    final nodesAsync = ref.watch(spaceNodesProvider(space.id));
    final selectedNode = ref.watch(nodeFilterProvider(space.id));

    return RefreshIndicator(
      onRefresh: () async => ref.invalidate(spaceTimelineProvider(space.id)),
      child: CustomScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        slivers: [
          SliverAppBar.large(
            title: Text(space.name),
            actions: [
              IconButton(
                tooltip: 'Search',
                icon: const Icon(Icons.search_rounded),
                onPressed: () => context.push('/spaces/search?in=${space.id}'),
              ),
            ],
          ),

          if (space.description case final d? when d.trim().isNotEmpty)
            SliverToBoxAdapter(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(Gap.lg, 0, Gap.lg, Gap.sm),
                child: Text(
                  d,
                  style: TextStyle(
                    fontSize: 13.5,
                    color: Theme.of(context).colorScheme.onSurfaceVariant,
                  ),
                ),
              ),
            ),

          if (nodesAsync.value case final nodes? when nodes.isNotEmpty)
            SliverToBoxAdapter(
              child: _NodeFilterStrip(
                spaceId: space.id,
                nodes: nodes,
                selected: selectedNode,
              ),
            ),

          timeline.when(
            loading: () => const SliverFillRemaining(
              hasScrollBody: false,
              child: LoadingState(),
            ),
            error: (e, _) => SliverFillRemaining(
              hasScrollBody: false,
              child: ErrorState(
                error: e,
                onRetry: () => ref.invalidate(spaceTimelineProvider(space.id)),
              ),
            ),
            data: (entries) => entries.isEmpty
                ? SliverFillRemaining(
                    hasScrollBody: false,
                    child: EmptyState(
                      icon: space.icon,
                      title: selectedNode == null
                          ? 'Nothing here yet'
                          : 'Nothing under this filter',
                      message: 'Tap + to add the first entry.',
                    ),
                  )
                : SliverPadding(
                    padding: const EdgeInsets.fromLTRB(Gap.lg, 0, Gap.lg, 96),
                    sliver: SliverList.separated(
                      itemCount: entries.length,
                      separatorBuilder: (_, _) => const SizedBox(height: Gap.md),
                      itemBuilder: (context, i) => _EntryCard(
                        spaceId: space.id,
                        entry: entries[i],
                      ),
                    ),
                  ),
          ),
        ],
      ),
    );
  }
}

/// Horizontally scrolling chips for the space's nodes, flattened rather than
/// nested — a phone-width indent-based tree would run out of room after two
/// levels, so depth is shown with a leading dash instead.
class _NodeFilterStrip extends ConsumerWidget {
  const _NodeFilterStrip({
    required this.spaceId,
    required this.nodes,
    required this.selected,
  });

  final String spaceId;
  final List<SpaceNode> nodes;
  final String? selected;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return SizedBox(
      height: 40,
      child: ListView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: Gap.lg),
        children: [
          _NodeChip(
            label: 'All',
            selected: selected == null,
            onTap: () => ref.read(nodeFilterProvider(spaceId).notifier).select(null),
          ),
          const SizedBox(width: Gap.sm),
          for (final node in nodes) ...[
            _NodeChip(
              label: node.depth > 0 ? '${'—' * node.depth} ${node.name}' : node.name,
              count: node.entryCount,
              selected: selected == node.id,
              onTap: () =>
                  ref.read(nodeFilterProvider(spaceId).notifier).select(node.id),
            ),
            const SizedBox(width: Gap.sm),
          ],
        ],
      ),
    );
  }
}

class _NodeChip extends StatelessWidget {
  const _NodeChip({
    required this.label,
    required this.selected,
    required this.onTap,
    this.count,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;
  final int? count;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return GestureDetector(
      onTap: onTap,
      child: Container(
        alignment: Alignment.center,
        padding: const EdgeInsets.symmetric(horizontal: Gap.md, vertical: 8),
        decoration: BoxDecoration(
          color: selected
              ? AppColors.accent.withValues(alpha: 0.15)
              : scheme.surfaceContainerHighest.withValues(alpha: 0.5),
          borderRadius: BorderRadius.circular(Radii.pill),
          border: Border.all(
            color: selected ? AppColors.accent : Colors.transparent,
            width: 1.4,
          ),
        ),
        child: Text(
          count == null ? label : '$label · $count',
          style: TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w600,
            color: selected ? AppColors.accent : scheme.onSurfaceVariant,
          ),
        ),
      ),
    );
  }
}

class _EntryCard extends StatelessWidget {
  const _EntryCard({required this.spaceId, required this.entry});

  final String spaceId;
  final EntryListItem entry;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return Card(
      child: InkWell(
        onTap: () => context.push('/spaces/$spaceId/entries/${entry.id}'),
        borderRadius: BorderRadius.circular(Radii.card),
        child: Padding(
          padding: const EdgeInsets.all(Gap.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      entry.title,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        fontSize: 15.5,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                  if (entry.timelineDate case final d?) ...[
                    const SizedBox(width: Gap.sm),
                    Text(
                      DateFormat('d MMM').format(d),
                      style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
                    ),
                  ],
                ],
              ),
              if (entry.excerpt.trim().isNotEmpty) ...[
                const SizedBox(height: 4),
                Text(
                  entry.excerpt,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 13.5,
                    height: 1.4,
                    color: scheme.onSurfaceVariant,
                  ),
                ),
              ],
              if (entry.nodePath != null || entry.tags.isNotEmpty) ...[
                const SizedBox(height: Gap.sm),
                Wrap(
                  spacing: Gap.sm,
                  runSpacing: 4,
                  children: [
                    if (entry.nodePath case final p? when p.isNotEmpty)
                      _Meta(icon: Icons.folder_outlined, label: p),
                    for (final tag in entry.tags.take(3))
                      _Meta(icon: Icons.sell_outlined, label: tag),
                  ],
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _Meta extends StatelessWidget {
  const _Meta({required this.icon, required this.label});

  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 12, color: scheme.onSurfaceVariant),
        const SizedBox(width: 3),
        Text(
          label,
          style: TextStyle(fontSize: 11.5, color: scheme.onSurfaceVariant),
        ),
      ],
    );
  }
}
