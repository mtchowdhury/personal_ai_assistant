import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:intl/intl.dart';

import '../../../core/theme/app_theme.dart';
import '../data/space_models.dart';

/// Renders one form control for a schema field, keyed by its type.
///
/// This is the piece that makes Spaces workable on mobile at all: the web
/// client renders everything but `select` as a bare text box, but a phone
/// keyboard is the expensive resource, so `number` gets a numeric pad, `date`
/// gets the native picker, and `tags` gets chips instead of a comma-separated
/// string someone has to type by hand.
class DynamicFieldInput extends StatelessWidget {
  const DynamicFieldInput({
    super.key,
    required this.field,
    required this.value,
    required this.onChanged,
  });

  final FieldSchema field;
  final Object? value;
  final ValueChanged<Object?> onChanged;

  @override
  Widget build(BuildContext context) {
    return switch (field.type) {
      FieldType.select => _SelectInput(field: field, value: value as String?, onChanged: onChanged),
      FieldType.date => _DateInput(field: field, value: value, onChanged: onChanged),
      FieldType.number => _NumberInput(field: field, value: value, onChanged: onChanged),
      FieldType.tags => _TagsInput(field: field, value: value, onChanged: onChanged),
      FieldType.text => _TextInput(field: field, value: value as String?, onChanged: onChanged),
    };
  }
}

class _FieldLabel extends StatelessWidget {
  const _FieldLabel(this.field);

  final FieldSchema field;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: 6, left: 2),
    child: Row(
      children: [
        Text(
          field.label,
          style: TextStyle(
            fontSize: 12.5,
            fontWeight: FontWeight.w600,
            color: Theme.of(context).colorScheme.onSurfaceVariant,
          ),
        ),
        if (field.required) ...[
          const SizedBox(width: 3),
          const Text(
            '*',
            style: TextStyle(color: AppColors.red, fontWeight: FontWeight.w700),
          ),
        ],
      ],
    ),
  );
}

class _TextInput extends StatefulWidget {
  const _TextInput({
    required this.field,
    required this.value,
    required this.onChanged,
  });

  final FieldSchema field;
  final String? value;
  final ValueChanged<Object?> onChanged;

  @override
  State<_TextInput> createState() => _TextInputState();
}

class _TextInputState extends State<_TextInput> {
  late final _controller = TextEditingController(text: widget.value ?? '');

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      _FieldLabel(widget.field),
      TextField(
        controller: _controller,
        textCapitalization: TextCapitalization.sentences,
        decoration: const InputDecoration(isDense: true),
        onChanged: (v) => widget.onChanged(v.isEmpty ? null : v),
      ),
    ],
  );
}

class _NumberInput extends StatefulWidget {
  const _NumberInput({
    required this.field,
    required this.value,
    required this.onChanged,
  });

  final FieldSchema field;
  final Object? value;
  final ValueChanged<Object?> onChanged;

  @override
  State<_NumberInput> createState() => _NumberInputState();
}

class _NumberInputState extends State<_NumberInput> {
  late final _controller = TextEditingController(
    text: widget.value == null ? '' : widget.value.toString(),
  );

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      _FieldLabel(widget.field),
      TextField(
        controller: _controller,
        keyboardType: const TextInputType.numberWithOptions(decimal: true),
        inputFormatters: [
          FilteringTextInputFormatter.allow(RegExp(r'[0-9.,\-]')),
        ],
        decoration: const InputDecoration(isDense: true),
        onChanged: (v) {
          final raw = v.trim().replaceAll(',', '.');
          widget.onChanged(raw.isEmpty ? null : double.tryParse(raw));
        },
      ),
    ],
  );
}

class _DateInput extends StatelessWidget {
  const _DateInput({
    required this.field,
    required this.value,
    required this.onChanged,
  });

  final FieldSchema field;
  final Object? value;
  final ValueChanged<Object?> onChanged;

  DateTime? get _date {
    if (value is DateTime) return value as DateTime;
    if (value is String) return DateTime.tryParse(value as String);
    return null;
  }

  @override
  Widget build(BuildContext context) {
    final d = _date;
    final scheme = Theme.of(context).colorScheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _FieldLabel(field),
        InkWell(
          borderRadius: BorderRadius.circular(Radii.card),
          onTap: () async {
            final picked = await showDatePicker(
              context: context,
              initialDate: d ?? DateTime.now(),
              firstDate: DateTime(1970),
              lastDate: DateTime(DateTime.now().year + 5),
            );
            if (picked != null) {
              onChanged(DateFormat('yyyy-MM-dd').format(picked));
            }
          },
          child: Container(
            width: double.infinity,
            padding: const EdgeInsets.symmetric(
              horizontal: Gap.lg,
              vertical: Gap.lg,
            ),
            decoration: BoxDecoration(
              color: Theme.of(context).cardTheme.color,
              borderRadius: BorderRadius.circular(Radii.card),
              border: Border.all(color: scheme.outlineVariant),
            ),
            child: Row(
              children: [
                Icon(Icons.event_rounded, size: 18, color: scheme.onSurfaceVariant),
                const SizedBox(width: Gap.sm),
                Text(
                  d == null ? 'Pick a date' : DateFormat('EEE d MMM y').format(d),
                  style: TextStyle(
                    color: d == null ? scheme.onSurfaceVariant : null,
                    fontWeight: d == null ? FontWeight.w400 : FontWeight.w600,
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _SelectInput extends StatelessWidget {
  const _SelectInput({
    required this.field,
    required this.value,
    required this.onChanged,
  });

  final FieldSchema field;
  final String? value;
  final ValueChanged<Object?> onChanged;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _FieldLabel(field),
        Wrap(
          spacing: Gap.sm,
          runSpacing: Gap.sm,
          children: [
            for (final option in field.options)
              GestureDetector(
                onTap: () => onChanged(value == option ? null : option),
                child: Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: Gap.md,
                    vertical: 8,
                  ),
                  decoration: BoxDecoration(
                    color: value == option
                        ? AppColors.accent.withValues(alpha: 0.15)
                        : scheme.surfaceContainerHighest.withValues(alpha: 0.5),
                    borderRadius: BorderRadius.circular(Radii.pill),
                    border: Border.all(
                      color: value == option
                          ? AppColors.accent
                          : Colors.transparent,
                      width: 1.4,
                    ),
                  ),
                  child: Text(
                    option,
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                      color: value == option
                          ? AppColors.accent
                          : scheme.onSurfaceVariant,
                    ),
                  ),
                ),
              ),
          ],
        ),
      ],
    );
  }
}

class _TagsInput extends StatefulWidget {
  const _TagsInput({
    required this.field,
    required this.value,
    required this.onChanged,
  });

  final FieldSchema field;
  final Object? value;
  final ValueChanged<Object?> onChanged;

  @override
  State<_TagsInput> createState() => _TagsInputState();
}

class _TagsInputState extends State<_TagsInput> {
  late List<String> _tags = _parse(widget.value);
  final _entryController = TextEditingController();

  static List<String> _parse(Object? v) {
    if (v is List) return v.map((e) => e.toString()).toList();
    if (v is String && v.trim().isNotEmpty) {
      return v.split(',').map((e) => e.trim()).where((e) => e.isNotEmpty).toList();
    }
    return [];
  }

  @override
  void dispose() {
    _entryController.dispose();
    super.dispose();
  }

  void _add(String raw) {
    final v = raw.trim();
    if (v.isEmpty || _tags.contains(v)) return;
    setState(() => _tags = [..._tags, v]);
    _entryController.clear();
    widget.onChanged(_tags);
  }

  void _remove(String tag) {
    setState(() => _tags = _tags.where((t) => t != tag).toList());
    widget.onChanged(_tags.isEmpty ? null : _tags);
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _FieldLabel(widget.field),
        Wrap(
          spacing: Gap.sm,
          runSpacing: Gap.sm,
          children: [
            for (final tag in _tags)
              Chip(
                label: Text(tag, style: const TextStyle(fontSize: 12.5)),
                onDeleted: () => _remove(tag),
                visualDensity: VisualDensity.compact,
                backgroundColor: scheme.surfaceContainerHighest,
                deleteIconColor: scheme.onSurfaceVariant,
                side: BorderSide.none,
              ),
            SizedBox(
              width: 140,
              child: TextField(
                controller: _entryController,
                decoration: const InputDecoration(
                  isDense: true,
                  hintText: 'Add…',
                  border: InputBorder.none,
                  enabledBorder: InputBorder.none,
                  focusedBorder: InputBorder.none,
                  filled: false,
                ),
                onSubmitted: _add,
              ),
            ),
          ],
        ),
      ],
    );
  }
}
