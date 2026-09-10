import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/app_states.dart';
import '../data/space_models.dart';
import '../data/spaces_controller.dart';

/// Search across every space at once, using the server's hybrid keyword +
/// semantic matching — the strongest thing the backend does that a phone
/// screen can showcase in one simple box.
class SpaceSearchScreen extends ConsumerStatefulWidget {
  const SpaceSearchScreen({super.key});

  @override
  ConsumerState<SpaceSearchScreen> createState() => _SpaceSearchScreenState();
}

class _SpaceSearchScreenState extends ConsumerState<SpaceSearchScreen> {
  final _controller = TextEditingController();
  Timer? _debounce;

  @override
  void dispose() {
    _debounce?.cancel();
    _controller.dispose();
    super.dispose();
  }

  void _onChanged(String value) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 400), () {
      ref.read(spaceSearchQueryProvider.notifier).set(value);
    });
  }

  @override
  Widget build(BuildContext context) {
    final query = ref.watch(spaceSearchQueryProvider);
    final async = ref.watch(spaceSearchResultsProvider);

    return Scaffold(
      appBar: AppBar(
        title: TextField(
          controller: _controller,
          autofocus: true,
          onChanged: _onChanged,
          textInputAction: TextInputAction.search,
          decoration: const InputDecoration(
            hintText: 'Search all spaces',
            border: InputBorder.none,
            enabledBorder: InputBorder.none,
            focusedBorder: InputBorder.none,
            filled: false,
          ),
        ),
      ),
      body: query.trim().isEmpty
          ? const EmptyState(
              icon: Icons.travel_explore_outlined,
              title: 'Search across everything',
              message:
                  'Matches on wording and meaning, across every space '
                  'at once.',
            )
          : async.when(
              loading: () => const LoadingState(),
              error: (e, _) => ErrorState(
                error: e,
                onRetry: () => ref.invalidate(spaceSearchResultsProvider),
              ),
              data: (results) => results.isEmpty
                  ? const EmptyState(
                      icon: Icons.search_off_rounded,
                      title: 'No matches',
                      message: 'Try different words.',
                    )
                  : ListView.separated(
                      padding: const EdgeInsets.all(Gap.lg),
                      itemCount: results.length,
                      separatorBuilder: (_, _) => const SizedBox(height: Gap.md),
                      itemBuilder: (context, i) => _ResultCard(result: results[i]),
                    ),
            ),
    );
  }
}

class _ResultCard extends StatelessWidget {
  const _ResultCard({required this.result});

  final SearchResult result;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return Card(
      child: InkWell(
        onTap: () => context.push(
          '/spaces/${result.spaceId}/entries/${result.entryId}',
        ),
        borderRadius: BorderRadius.circular(Radii.card),
        child: Padding(
          padding: const EdgeInsets.all(Gap.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 2),
                    decoration: BoxDecoration(
                      color: AppColors.accent.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(Radii.pill),
                    ),
                    child: Text(
                      result.spaceName,
                      style: const TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w700,
                        color: AppColors.accent,
                      ),
                    ),
                  ),
                  if (result.occurredOn case final d?) ...[
                    const Spacer(),
                    Text(
                      DateFormat('d MMM y').format(d),
                      style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
                    ),
                  ],
                ],
              ),
              const SizedBox(height: Gap.sm),
              Text(
                result.title,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(fontSize: 15.5, fontWeight: FontWeight.w700),
              ),
              if (result.snippet.trim().isNotEmpty) ...[
                const SizedBox(height: 3),
                Text(
                  result.snippet,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 13.5,
                    height: 1.4,
                    color: scheme.onSurfaceVariant,
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
