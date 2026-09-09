import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/app_states.dart';
import '../data/finance_controller.dart';
import '../data/finance_models.dart';

/// One expense: header, receipt, and line items.
class ExpenseDetailScreen extends ConsumerWidget {
  const ExpenseDetailScreen({super.key, required this.expenseId});

  final String expenseId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(expenseDetailProvider(expenseId));

    return Scaffold(
      appBar: AppBar(
        actions: [
          if (async.hasValue)
            IconButton(
              tooltip: 'Delete',
              icon: const Icon(Icons.delete_outline_rounded),
              onPressed: () => _confirmDelete(context, ref),
            ),
        ],
      ),
      body: async.when(
        loading: () => const LoadingState(),
        error: (e, _) => ErrorState(
          error: e,
          onRetry: () => ref.invalidate(expenseDetailProvider(expenseId)),
        ),
        data: (expense) => _Body(expenseId: expenseId, expense: expense),
      ),
    );
  }

  Future<void> _confirmDelete(BuildContext context, WidgetRef ref) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Delete expense?'),
        content: const Text('This cannot be undone.'),
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
    if (ok != true) return;

    try {
      await ref.read(expenseDetailProvider(expenseId).notifier).delete();
      if (context.mounted) Navigator.of(context).pop();
    } on ApiException catch (e) {
      if (context.mounted) showAppSnack(context, e.message, error: true);
    }
  }
}

class _Body extends ConsumerWidget {
  const _Body({required this.expenseId, required this.expense});

  final String expenseId;
  final ExpenseDetail expense;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final e = expense;
    final scheme = Theme.of(context).colorScheme;

    return ListView(
      padding: const EdgeInsets.fromLTRB(Gap.lg, 0, Gap.lg, Gap.xxl),
      children: [
        Text(
          formatMoney(e.totalAmount, e.currency),
          textAlign: TextAlign.center,
          style: const TextStyle(
            fontSize: 40,
            fontWeight: FontWeight.w800,
            letterSpacing: -1.5,
            height: 1.1,
          ),
        ),
        const SizedBox(height: Gap.xs),
        Text(
          e.shopName?.trim().isNotEmpty == true
              ? e.shopName!
              : (e.categoryName ?? 'Expense'),
          textAlign: TextAlign.center,
          style: TextStyle(fontSize: 15, color: scheme.onSurfaceVariant),
        ),
        const SizedBox(height: Gap.xl),

        Card(
          child: Column(
            children: [
              _Row(
                icon: Icons.event_rounded,
                label: 'Date',
                value: DateFormat('EEE d MMM y').format(e.purchasedOn),
              ),
              if (e.categoryName != null) ...[
                const Divider(indent: 52),
                _Row(
                  icon: Icons.sell_outlined,
                  label: 'Category',
                  value: e.categoryName!,
                ),
              ],
              if (e.paymentMethod case final p? when p.isNotEmpty) ...[
                const Divider(indent: 52),
                _Row(
                  icon: Icons.credit_card_rounded,
                  label: 'Paid with',
                  value: p,
                ),
              ],
              if (e.source != 'manual') ...[
                const Divider(indent: 52),
                _Row(
                  icon: Icons.auto_awesome_rounded,
                  label: 'Added by',
                  value: e.source,
                ),
              ],
            ],
          ),
        ),

        if (e.notes case final n? when n.trim().isNotEmpty) ...[
          const SizedBox(height: Gap.lg),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(Gap.md),
              child: Text(
                n,
                style: const TextStyle(fontSize: 14.5, height: 1.45),
              ),
            ),
          ),
        ],

        if (e.items.isNotEmpty) ...[
          const SizedBox(height: Gap.lg),
          _Label('Items  ${e.items.length}'),
          Card(
            child: Column(
              children: [
                for (final (i, item) in e.items.indexed) ...[
                  if (i > 0) const Divider(indent: Gap.md),
                  _ItemRow(item: item, currency: e.currency),
                ],
                // A receipt whose lines do not add up to its total is worth
                // flagging — usually the AI missed a line or a discount.
                if (!e.itemsMatchTotal) ...[
                  const Divider(indent: Gap.md),
                  Padding(
                    padding: const EdgeInsets.all(Gap.md),
                    child: Row(
                      children: [
                        const Icon(
                          Icons.info_outline_rounded,
                          size: 15,
                          color: AppColors.amber,
                        ),
                        const SizedBox(width: Gap.sm),
                        Expanded(
                          child: Text(
                            'Items total ${formatMoney(e.itemsTotal, e.currency)}, '
                            'which differs from the receipt total.',
                            style: const TextStyle(
                              fontSize: 12,
                              color: AppColors.amber,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ],
            ),
          ),
        ],

        const SizedBox(height: Gap.lg),
        _Label('Receipt'),
        _Receipt(expenseId: expenseId, hasImage: e.hasImage),
      ],
    );
  }
}

/// The receipt image, or a control to attach one.
///
/// The image endpoint requires the bearer token, so the bytes come through
/// Dio rather than `Image.network`, which cannot set a header.
class _Receipt extends ConsumerWidget {
  const _Receipt({required this.expenseId, required this.hasImage});

  final String expenseId;
  final bool hasImage;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (!hasImage) {
      return Card(
        child: Padding(
          padding: const EdgeInsets.all(Gap.md),
          child: Row(
            children: [
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: () => _attach(context, ref, ImageSource.camera),
                  icon: const Icon(Icons.photo_camera_outlined, size: 18),
                  label: const Text('Snap receipt'),
                ),
              ),
              const SizedBox(width: Gap.sm),
              OutlinedButton(
                onPressed: () => _attach(context, ref, ImageSource.gallery),
                child: const Icon(Icons.photo_library_outlined, size: 18),
              ),
            ],
          ),
        ),
      );
    }

    final bytes = ref.watch(receiptProvider(expenseId));

    return Card(
      clipBehavior: Clip.antiAlias,
      child: bytes.when(
        loading: () => const SizedBox(height: 200, child: LoadingState()),
        error: (e, _) => SizedBox(
          height: 160,
          child: ErrorState(
            error: e,
            onRetry: () => ref.invalidate(receiptProvider(expenseId)),
          ),
        ),
        data: (data) => data == null
            ? const SizedBox(
                height: 120,
                child: Center(child: Text('Receipt image missing')),
              )
            : GestureDetector(
                // Full screen, because a receipt is unreadable at card size.
                onTap: () => showDialog<void>(
                  context: context,
                  builder: (dialogContext) => Dialog.fullscreen(
                    backgroundColor: Colors.black,
                    child: Stack(
                      children: [
                        Center(
                          child: InteractiveViewer(
                            maxScale: 5,
                            child: Image.memory(data),
                          ),
                        ),
                        SafeArea(
                          child: IconButton(
                            icon: const Icon(
                              Icons.close_rounded,
                              color: Colors.white,
                            ),
                            onPressed: () =>
                                Navigator.of(dialogContext).pop(),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
                child: Image.memory(
                  data,
                  fit: BoxFit.cover,
                  width: double.infinity,
                  height: 220,
                ),
              ),
      ),
    );
  }

  Future<void> _attach(
    BuildContext context,
    WidgetRef ref,
    ImageSource source,
  ) async {
    try {
      final picked = await ImagePicker().pickImage(
        source: source,
        maxWidth: 2000,
        imageQuality: 85,
      );
      if (picked == null) return;

      await ref
          .read(expenseDetailProvider(expenseId).notifier)
          .attachReceipt(picked.path, picked.name);

      if (context.mounted) showAppSnack(context, 'Receipt attached');
    } on ApiException catch (e) {
      if (context.mounted) showAppSnack(context, e.message, error: true);
    } catch (_) {
      if (context.mounted) {
        showAppSnack(context, 'Could not attach that image.', error: true);
      }
    }
  }
}

class _ItemRow extends StatelessWidget {
  const _ItemRow({required this.item, required this.currency});

  final ExpenseItem item;
  final String currency;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    // Quantity is only worth showing when it is not a plain single unit.
    final showQty = item.quantity != 1 && item.quantity > 0;

    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: Gap.md,
        vertical: Gap.md,
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  item.displayName,
                  style: const TextStyle(
                    fontSize: 14.5,
                    fontWeight: FontWeight.w500,
                  ),
                ),
                if (showQty)
                  Text(
                    '${_qty(item.quantity)} × '
                    '${formatMoney(item.unitPrice, currency)}',
                    style: TextStyle(
                      fontSize: 12,
                      color: scheme.onSurfaceVariant,
                    ),
                  ),
              ],
            ),
          ),
          const SizedBox(width: Gap.sm),
          Text(
            formatMoney(item.lineTotal, currency),
            style: const TextStyle(fontSize: 14.5, fontWeight: FontWeight.w600),
          ),
        ],
      ),
    );
  }

  /// Drops a trailing `.0` so whole quantities read as "2", not "2.0".
  static String _qty(double q) =>
      q == q.roundToDouble() ? q.toStringAsFixed(0) : q.toString();
}

class _Label extends StatelessWidget {
  const _Label(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(left: Gap.xs, bottom: Gap.sm),
    child: Text(
      text.toUpperCase(),
      style: TextStyle(
        fontSize: 11.5,
        fontWeight: FontWeight.w800,
        letterSpacing: 0.7,
        color: Theme.of(context).colorScheme.onSurfaceVariant,
      ),
    ),
  );
}

class _Row extends StatelessWidget {
  const _Row({required this.icon, required this.label, required this.value});

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: Gap.md,
        vertical: Gap.md + 2,
      ),
      child: Row(
        children: [
          Icon(icon, size: 20, color: scheme.onSurfaceVariant),
          const SizedBox(width: Gap.md),
          Text(
            label,
            style: TextStyle(fontSize: 14, color: scheme.onSurfaceVariant),
          ),
          const Spacer(),
          Flexible(
            child: Text(
              value,
              textAlign: TextAlign.end,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontSize: 14.5,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
