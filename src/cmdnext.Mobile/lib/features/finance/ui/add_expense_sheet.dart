import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/app_states.dart';
import '../data/finance_controller.dart';
import '../data/finance_models.dart';
import '../data/finance_repository.dart';

/// Logs an expense. Returns true if one was created.
Future<bool> showAddExpenseSheet(BuildContext context) async {
  final created = await showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    builder: (_) => const _AddExpenseSheet(),
  );
  return created ?? false;
}

class _AddExpenseSheet extends ConsumerStatefulWidget {
  const _AddExpenseSheet();

  @override
  ConsumerState<_AddExpenseSheet> createState() => _AddExpenseSheetState();
}

class _AddExpenseSheetState extends ConsumerState<_AddExpenseSheet> {
  final _amount = TextEditingController();
  final _shop = TextEditingController();
  final _amountFocus = FocusNode();

  DateTime _date = DateTime.now();
  String? _categoryId;
  XFile? _receipt;
  bool _saving = false;

  @override
  void dispose() {
    _amount.dispose();
    _shop.dispose();
    _amountFocus.dispose();
    super.dispose();
  }

  Money? get _parsedAmount {
    // Accept a comma decimal separator — the keyboard offers it in most of
    // Europe, and rejecting it silently would be baffling.
    final raw = _amount.text.trim().replaceAll(',', '.');
    final v = double.tryParse(raw);
    return (v == null || v <= 0) ? null : v;
  }

  Future<void> _pickReceipt(ImageSource source) async {
    try {
      final picked = await ImagePicker().pickImage(
        source: source,
        // The server caps uploads at 10 MB; a full-resolution photo can exceed
        // that, so it is downscaled before it ever leaves the device.
        maxWidth: 2000,
        imageQuality: 85,
      );
      if (picked != null) setState(() => _receipt = picked);
    } catch (e) {
      if (mounted) {
        showAppSnack(context, 'Could not open the camera.', error: true);
      }
    }
  }

  Future<void> _save() async {
    final amount = _parsedAmount;
    if (amount == null || _saving) return;

    setState(() => _saving = true);
    final repo = ref.read(financeRepositoryProvider);

    try {
      final expense = await repo.createExpense(
        shopName: _shop.text,
        purchasedOn: _date,
        totalAmount: amount,
        categoryId: _categoryId,
      );

      // The receipt is a second call: the image endpoint needs an expense to
      // hang off. A failure here must not lose the expense itself, so it is
      // reported separately rather than failing the whole save.
      if (_receipt case final r?) {
        try {
          await repo.uploadReceipt(
            expenseId: expense.id,
            filePath: r.path,
            fileName: r.name,
          );
        } on ApiException catch (e) {
          if (mounted) {
            showAppSnack(
              context,
              'Expense saved, but the receipt did not upload: ${e.message}',
              error: true,
            );
          }
        }
      }

      ref.invalidate(financeDashboardProvider);
      ref.invalidate(expenseListProvider);

      if (mounted) {
        HapticFeedback.mediumImpact();
        Navigator.of(context).pop(true);
      }
    } on ApiException catch (e) {
      if (mounted) {
        setState(() => _saving = false);
        showAppSnack(context, e.message, error: true);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final categories = ref.watch(expenseCategoriesProvider).value ?? const [];
    final scheme = Theme.of(context).colorScheme;

    return Padding(
      padding: EdgeInsets.only(
        left: Gap.lg,
        right: Gap.lg,
        top: Gap.sm,
        bottom: MediaQuery.viewInsetsOf(context).bottom + Gap.lg,
      ),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Amount leads: it is the one field an expense cannot do without.
            TextField(
              controller: _amount,
              focusNode: _amountFocus,
              autofocus: true,
              keyboardType: const TextInputType.numberWithOptions(
                decimal: true,
              ),
              inputFormatters: [
                FilteringTextInputFormatter.allow(RegExp(r'[0-9.,]')),
              ],
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 40,
                fontWeight: FontWeight.w800,
                letterSpacing: -1.5,
              ),
              decoration: const InputDecoration(
                hintText: '0.00',
                border: InputBorder.none,
                enabledBorder: InputBorder.none,
                focusedBorder: InputBorder.none,
                filled: false,
                contentPadding: EdgeInsets.symmetric(vertical: Gap.sm),
              ),
              onChanged: (_) => setState(() {}),
            ),

            const SizedBox(height: Gap.sm),
            TextField(
              controller: _shop,
              textCapitalization: TextCapitalization.words,
              decoration: const InputDecoration(
                hintText: 'Where?',
                prefixIcon: Icon(Icons.storefront_outlined),
                isDense: true,
              ),
            ),

            const SizedBox(height: Gap.md),
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: [
                  _Chip(
                    label: _dateLabel(),
                    icon: Icons.event_rounded,
                    selected: true,
                    onTap: _pickDate,
                  ),
                  const SizedBox(width: Gap.sm),
                  for (final c in categories) ...[
                    _Chip(
                      label: c.name,
                      icon: c.icon,
                      selected: _categoryId == c.id,
                      onTap: () => setState(
                        () => _categoryId = _categoryId == c.id ? null : c.id,
                      ),
                    ),
                    const SizedBox(width: Gap.sm),
                  ],
                ],
              ),
            ),

            const SizedBox(height: Gap.md),
            if (_receipt != null)
              Row(
                children: [
                  const Icon(
                    Icons.check_circle_rounded,
                    size: 18,
                    color: AppColors.green,
                  ),
                  const SizedBox(width: Gap.sm),
                  Expanded(
                    child: Text(
                      'Receipt attached',
                      style: TextStyle(
                        fontSize: 13,
                        fontWeight: FontWeight.w600,
                        color: scheme.onSurface,
                      ),
                    ),
                  ),
                  TextButton(
                    onPressed: () => setState(() => _receipt = null),
                    child: const Text('Remove'),
                  ),
                ],
              )
            else
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: () => _pickReceipt(ImageSource.camera),
                      icon: const Icon(Icons.photo_camera_outlined, size: 18),
                      label: const Text('Snap receipt'),
                    ),
                  ),
                  const SizedBox(width: Gap.sm),
                  OutlinedButton(
                    onPressed: () => _pickReceipt(ImageSource.gallery),
                    child: const Icon(Icons.photo_library_outlined, size: 18),
                  ),
                ],
              ),

            const SizedBox(height: Gap.lg),
            FilledButton(
              onPressed: _parsedAmount == null || _saving ? null : _save,
              child: _saving
                  ? const SizedBox(
                      height: 20,
                      width: 20,
                      child: CircularProgressIndicator(
                        strokeWidth: 2.2,
                        color: Colors.white,
                      ),
                    )
                  : const Text('Save expense'),
            ),
          ],
        ),
      ),
    );
  }

  String _dateLabel() {
    final now = DateTime.now();
    final d = DateTime(_date.year, _date.month, _date.day);
    final t = DateTime(now.year, now.month, now.day);
    if (d == t) return 'Today';
    if (d == t.subtract(const Duration(days: 1))) return 'Yesterday';
    return DateFormat('d MMM').format(_date);
  }

  Future<void> _pickDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _date,
      firstDate: DateTime(DateTime.now().year - 3),
      lastDate: DateTime.now(),
    );
    if (picked != null) {
      // Keep the current time of day: purchasedOn is a real instant here.
      setState(
        () => _date = DateTime(
          picked.year,
          picked.month,
          picked.day,
          _date.hour,
          _date.minute,
        ),
      );
    }
  }
}

class _Chip extends StatelessWidget {
  const _Chip({
    required this.label,
    required this.selected,
    required this.onTap,
    this.icon,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return GestureDetector(
      onTap: onTap,
      child: Container(
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
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (icon != null) ...[
              Icon(
                icon,
                size: 14,
                color: selected ? AppColors.accent : scheme.onSurfaceVariant,
              ),
              const SizedBox(width: 5),
            ],
            Text(
              label,
              style: TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w600,
                color: selected ? AppColors.accent : scheme.onSurfaceVariant,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
