import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import 'package:open_filex/open_filex.dart';
import 'package:path_provider/path_provider.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/app_states.dart';
import '../data/space_models.dart';
import '../data/spaces_controller.dart';
import '../data/spaces_repository.dart';
import 'dynamic_field_input.dart';

/// One entry, with its schema fields editable in place.
///
/// The fields shown are whatever the entry's type declares — the same schema
/// that drove the create form drives this one, so a journal entry's mood/place
/// rating gets a proper control here too, not just a raw JSON dump.
class EntryDetailScreen extends ConsumerWidget {
  const EntryDetailScreen({
    super.key,
    required this.spaceId,
    required this.entryId,
  });

  final String spaceId;
  final String entryId;

  EntryRef get _ref => (spaceId: spaceId, entryId: entryId);

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(entryDetailProvider(_ref));
    final spaceAsync = ref.watch(spaceProvider(spaceId));

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
          onRetry: () => ref.invalidate(entryDetailProvider(_ref)),
        ),
        data: (entry) => _Body(
          spaceId: spaceId,
          entryId: entryId,
          entry: entry,
          schema: spaceAsync.value?.typeFor(entry.type),
        ),
      ),
    );
  }

  Future<void> _confirmDelete(BuildContext context, WidgetRef ref) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Delete entry?'),
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
      await ref.read(entryDetailProvider(_ref).notifier).delete();
      if (context.mounted) Navigator.of(context).pop();
    } on ApiException catch (e) {
      if (context.mounted) showAppSnack(context, e.message, error: true);
    }
  }
}

class _Body extends ConsumerStatefulWidget {
  const _Body({
    required this.spaceId,
    required this.entryId,
    required this.entry,
    required this.schema,
  });

  final String spaceId;
  final String entryId;
  final Entry entry;

  /// Null when the space's schema has since dropped this entry type, or the
  /// space itself has not loaded yet — the raw fields still render either way.
  final EntryTypeSchema? schema;

  @override
  ConsumerState<_Body> createState() => _BodyState();
}

class _BodyState extends ConsumerState<_Body> {
  late final _title = TextEditingController(text: widget.entry.title);
  late final _body = TextEditingController(text: widget.entry.body);
  /// Drives whether the formatting bar is shown — it belongs above the keyboard
  /// while the body is being edited, not permanently on the screen.
  final _bodyFocus = FocusNode();

  /// Groups the body field and its formatting bar into one tap region, so a
  /// tap on the bar does not read as a tap outside the field.
  final _bodyTapGroup = Object();
  late final Map<String, Object?> _fieldValues = Map.of(widget.entry.fields);

  EntryRef get _ref => (spaceId: widget.spaceId, entryId: widget.entryId);

  /// Matches a markdown task line: "- [ ] text" / "1. [x] text".
  static final _taskLine = RegExp(r'^(\s*(?:[-*+]|\d+[.)])\s+\[)([ xX])(\]\s+)(.*)$');

  /// The task items in the current body text, in document order.
  List<({int line, bool checked, String text})> get _tasks {
    final lines = _body.text.split('\n');
    final out = <({int line, bool checked, String text})>[];
    for (var i = 0; i < lines.length; i++) {
      final m = _taskLine.firstMatch(lines[i]);
      if (m == null) continue;
      out.add((
        line: i,
        checked: m.group(2)!.toLowerCase() == 'x',
        text: m.group(4)!,
      ));
    }
    return out;
  }

  // ---- Formatting toolbar ----
  //
  // Writes plain markdown into the field, exactly as the web toolbar does, so
  // the stored format stays what the search index, the AI tools and the web
  // client already read.

  /// Wraps the selection in [marker] (bold, italic, code), or unwraps it when
  /// the markers are already there so the button toggles.
  void _wrapSelection(String marker) {
    final sel = _body.selection;
    if (!sel.isValid) return;

    final text = _body.text;
    final selected = text.substring(sel.start, sel.end);
    final before = text.substring(0, sel.start);
    final after = text.substring(sel.end);

    if (before.endsWith(marker) && after.startsWith(marker)) {
      final updated = before.substring(0, before.length - marker.length) +
          selected +
          after.substring(marker.length);
      _apply(updated, sel.start - marker.length, sel.end - marker.length);
      return;
    }

    final placeholder = selected.isEmpty ? 'text' : selected;
    _apply(
      '$before$marker$placeholder$marker$after',
      sel.start + marker.length,
      sel.start + marker.length + placeholder.length,
    );
  }

  /// Applies a line prefix (heading, bullet, number, task, quote) to every line
  /// the selection touches; re-applying the same prefix strips it.
  void _prefixLines(String prefix) {
    final sel = _body.selection;
    if (!sel.isValid) return;

    final text = _body.text;
    // Dart's lastIndexOf throws on a negative start (JS returns -1), so guard
    // the caret-at-0 case rather than letting it crash.
    final from = sel.start == 0 ? 0 : text.lastIndexOf('\n', sel.start - 1) + 1;
    final toIndex = text.indexOf('\n', sel.end);
    final to = toIndex == -1 ? text.length : toIndex;

    final lines = text.substring(from, to).split('\n');
    final isNumbered = prefix == '1. ';
    final existing = isNumbered ? RegExp(r'^\d+[.)]\s+') : null;

    // Only lines with content decide a toggle-off; a blank line would satisfy
    // `every` vacuously and make the button a no-op on an empty line.
    final contentLines = lines.where((l) => l.trim().isNotEmpty);
    final allPrefixed = contentLines.isNotEmpty &&
        contentLines.every(
          (l) => existing != null ? existing.hasMatch(l) : l.startsWith(prefix),
        );

    final stripAny = RegExp(
      r'^(#{1,6}\s+|[-*+]\s+\[[ xX]\]\s+|[-*+]\s+|\d+[.)]\s+|>\s+)',
    );

    final updated = <String>[];
    for (var i = 0; i < lines.length; i++) {
      final line = lines[i];
      if (allPrefixed) {
        if (line.trim().isEmpty) {
          updated.add(line);
        } else {
          updated.add(
            existing != null
                ? line.replaceFirst(existing, '')
                : line.substring(prefix.length),
          );
        }
        continue;
      }
      if (line.trim().isEmpty) {
        // A lone empty line becomes a bare marker to type into.
        updated.add(lines.length == 1 ? (isNumbered ? '1. ' : prefix) : line);
        continue;
      }
      final bare = line.replaceFirst(stripAny, '');
      updated.add('${isNumbered ? '${i + 1}. ' : prefix}$bare');
    }

    final replacement = updated.join('\n');
    final result = text.substring(0, from) + replacement + text.substring(to);

    // Starting a fresh list puts the caret after the marker, ready to type.
    if (lines.length == 1 && lines[0].trim().isEmpty && !allPrefixed) {
      final caret = from + replacement.length;
      _apply(result, caret, caret);
    } else {
      _apply(result, from, from + replacement.length);
    }
  }

  /// The formatting bar. Buttons act on the field's current selection and are
  /// wrapped so the field never loses focus (and the keyboard never dismisses)
  /// when one is tapped.
  Widget _buildFormatBar(ColorScheme scheme) {
    Widget button(String label, VoidCallback onTap, {String? tooltip}) {
      final child = InkWell(
        borderRadius: BorderRadius.circular(8),
        onTap: () {
          onTap();
          // The field keeps focus via the TapRegion below, but re-assert it so
          // the keyboard stays up even if something else claimed focus.
          if (!_bodyFocus.hasFocus) _bodyFocus.requestFocus();
        },
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
          child: Text(
            label,
            style: TextStyle(
              fontSize: 15,
              height: 1.1,
              color: scheme.onSurface,
            ),
          ),
        ),
      );
      return tooltip == null ? child : Tooltip(message: tooltip, child: child);
    }

    // Same groupId as the body field: TextField.onTapOutside only fires for taps
    // outside its own tap region, so sharing the group makes a tap on this bar
    // count as inside — the field keeps focus and the keyboard stays up.
    // (Setting a flag from onTapDown does not work: onTapOutside fires on
    // pointer-down, before any tap callback, and once the field unfocuses the
    // bar is removed from the tree and onTap never arrives.)
    return TapRegion(
      groupId: _bodyTapGroup,
      child: Padding(
      padding: const EdgeInsets.only(top: Gap.sm),
      child: Material(
        color: scheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(10),
        child: SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          padding: const EdgeInsets.symmetric(horizontal: Gap.xs),
          child: Row(
            children: [
              button('B', () => _wrapSelection('**'), tooltip: 'Bold'),
              button('I', () => _wrapSelection('*'), tooltip: 'Italic'),
              button('< >', () => _wrapSelection('`'), tooltip: 'Code'),
              const SizedBox(width: Gap.xs),
              button('H', () => _prefixLines('### '), tooltip: 'Heading'),
              button('•', () => _prefixLines('- '), tooltip: 'Bullet list'),
              button('1.', () => _prefixLines('1. '), tooltip: 'Numbered list'),
              button('☑', () => _prefixLines('- [ ] '), tooltip: 'Checklist'),
              const SizedBox(width: Gap.xs),
              button('❝', () => _prefixLines('> '), tooltip: 'Quote'),
            ],
          ),
        ),
      ),
      ),
    );
  }

  /// Writes new text and selection in one go so the field does not bounce the
  /// caret to the end.
  void _apply(String text, int start, int end) {
    _body.value = TextEditingValue(
      text: text,
      selection: TextSelection(baseOffset: start, extentOffset: end),
    );
  }

  /// Enter inside a list item continues the list; Enter on an item that is
  /// still empty ends it instead. Mirrors the web editor so the two behave the
  /// same way.
  static final _listLine = RegExp(
    r'^(\s*)(?:([-*+])\s+\[[ xX]\]\s+|([-*+])\s+|(\d+)([.)])\s+)(.*)$',
  );

  /// Returns the replacement text + caret when Enter should be handled here, or
  /// null to let the field insert a plain newline.
  ({String text, int caret})? _listContinuation() {
    final selection = _body.selection;
    if (!selection.isValid || !selection.isCollapsed) return null;

    final caret = selection.baseOffset;
    final text = _body.text;
    // See _prefixLines: lastIndexOf throws on a negative start index.
    final lineStart = caret == 0 ? 0 : text.lastIndexOf('\n', caret - 1) + 1;
    final match = _listLine.firstMatch(text.substring(lineStart, caret));
    if (match == null) return null;

    final indent = match.group(1)!;
    final content = match.group(6)!;

    // Empty item: strip the marker and end the list.
    if (content.trim().isEmpty) {
      return (
        text: text.substring(0, lineStart) + text.substring(caret),
        caret: lineStart,
      );
    }

    final String marker;
    if (match.group(2) != null) {
      marker = '${match.group(2)} [ ] ';
    } else if (match.group(3) != null) {
      marker = '${match.group(3)} ';
    } else {
      marker = '${int.parse(match.group(4)!) + 1}${match.group(5)} ';
    }

    final insert = '\n$indent$marker';
    return (
      text: text.substring(0, caret) + insert + text.substring(caret),
      caret: caret + insert.length,
    );
  }

  /// Flips one task marker in the markdown and saves. Keeping the body as
  /// markdown means the checklist and the raw text never diverge.
  Future<void> _toggleTask(int lineIndex) async {
    final lines = _body.text.split('\n');
    if (lineIndex < 0 || lineIndex >= lines.length) return;

    final m = _taskLine.firstMatch(lines[lineIndex]);
    if (m == null) return;

    final checked = m.group(2)!.toLowerCase() == 'x';
    lines[lineIndex] = '${m.group(1)}${checked ? ' ' : 'x'}${m.group(3)}${m.group(4)}';
    final updated = lines.join('\n');

    _body.text = updated;
    await _save(
      () => ref.read(entryDetailProvider(_ref).notifier).save(body: updated),
    );
  }

  @override
  void initState() {
    super.initState();
    // The checklist is derived from the body text, so it has to rebuild as the
    // raw markdown is edited (adding "- [ ] " should make a row appear).
    _body.addListener(_onBodyChanged);
    _bodyFocus.addListener(_onBodyFocusChanged);
  }

  void _onBodyFocusChanged() {
    if (mounted) setState(() {});
  }

  /// Rebuilds only when the set of task lines actually changes — every
  /// keystroke would otherwise setState the whole screen.
  String _taskSignature = '';

  void _onBodyChanged() {
    final signature = _tasks.map((t) => '${t.line}:${t.checked}').join('|');
    if (signature == _taskSignature) return;
    _taskSignature = signature;
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    _body.removeListener(_onBodyChanged);
    _bodyFocus.removeListener(_onBodyFocusChanged);
    _bodyFocus.dispose();
    _title.dispose();
    _body.dispose();
    super.dispose();
  }

  Future<void> _save(Future<void> Function() action) async {
    try {
      await action();
    } on ApiException catch (e) {
      if (mounted) showAppSnack(context, e.message, error: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final entry = widget.entry;
    final scheme = Theme.of(context).colorScheme;
    final controller = ref.read(entryDetailProvider(_ref).notifier);

    // Fields the schema declares, in schema order — plus any leftover values
    // from a since-removed field, shown as read-only so nothing is silently
    // dropped by editing.
    final schemaFields = widget.schema?.fields ?? const <FieldSchema>[];
    final schemaNames = schemaFields.map((f) => f.name).toSet();
    final orphanFields = entry.fields.keys.where((k) => !schemaNames.contains(k));

    return ListView(
      padding: const EdgeInsets.fromLTRB(Gap.lg, Gap.md, Gap.lg, Gap.xxl),
      children: [
        TextField(
          controller: _title,
          maxLines: null,
          textCapitalization: TextCapitalization.sentences,
          style: const TextStyle(
            fontSize: 21,
            fontWeight: FontWeight.w700,
            letterSpacing: -0.3,
          ),
          decoration: const InputDecoration(
            border: InputBorder.none,
            enabledBorder: InputBorder.none,
            focusedBorder: InputBorder.none,
            filled: false,
            isDense: true,
            contentPadding: EdgeInsets.symmetric(vertical: Gap.sm),
          ),
          onTapOutside: (_) {
            FocusScope.of(context).unfocus();
            final v = _title.text.trim();
            if (v.isNotEmpty && v != entry.title) {
              _save(() => controller.save(title: v));
            }
          },
        ),

        Row(
          children: [
            if (entry.nodePath case final p? when p.isNotEmpty) ...[
              Icon(Icons.folder_outlined, size: 13, color: scheme.onSurfaceVariant),
              const SizedBox(width: 4),
              Text(p, style: TextStyle(fontSize: 12.5, color: scheme.onSurfaceVariant)),
              const SizedBox(width: Gap.md),
            ],
            if (entry.occurredOn case final d?)
              Text(
                DateFormat('EEE d MMM y').format(d),
                style: TextStyle(fontSize: 12.5, color: scheme.onSurfaceVariant),
              ),
          ],
        ),

        // Checklist view of the markdown task lines. Imported notes carry a lot
        // of "- [ ]" items; tapping one here rewrites the marker in the body
        // rather than making the user hunt for it in the raw text.
        if (_tasks.isNotEmpty) ...[
          const SizedBox(height: Gap.lg),
          Card(
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: Gap.xs),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  for (final task in _tasks)
                    InkWell(
                      onTap: () => _toggleTask(task.line),
                      child: Padding(
                        padding: const EdgeInsets.symmetric(
                          horizontal: Gap.sm,
                          vertical: 2,
                        ),
                        child: Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Checkbox(
                              value: task.checked,
                              visualDensity: VisualDensity.compact,
                              materialTapTargetSize:
                                  MaterialTapTargetSize.shrinkWrap,
                              onChanged: (_) => _toggleTask(task.line),
                            ),
                            const SizedBox(width: Gap.xs),
                            Expanded(
                              child: Padding(
                                padding: const EdgeInsets.only(top: 10),
                                child: Text(
                                  task.text,
                                  style: TextStyle(
                                    fontSize: 14.5,
                                    height: 1.35,
                                    decoration: task.checked
                                        ? TextDecoration.lineThrough
                                        : null,
                                    color: task.checked
                                        ? scheme.onSurfaceVariant
                                        : null,
                                  ),
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                ],
              ),
            ),
          ),
        ],

        const SizedBox(height: Gap.lg),
        Card(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: Gap.md, vertical: Gap.sm),
            // Same tap group as the formatting bar: TextField.onTapOutside only
            // fires for taps outside its own tap region, so grouping the two
            // makes a tap on the bar count as inside and the field keeps focus.
            child: TapRegion(
            groupId: _bodyTapGroup,
            // Enter is intercepted before the field sees it so a list item can
            // continue with the same marker (or end the list when empty).
            child: CallbackShortcuts(
              bindings: {
                const SingleActivator(LogicalKeyboardKey.enter): () {
                  final next = _listContinuation();
                  if (next == null) {
                    // Not in a list — insert the newline the field would have.
                    final sel = _body.selection;
                    if (!sel.isValid) return;
                    final text = _body.text.replaceRange(
                      sel.start,
                      sel.end,
                      '\n',
                    );
                    _body.value = TextEditingValue(
                      text: text,
                      selection: TextSelection.collapsed(offset: sel.start + 1),
                    );
                    return;
                  }
                  _body.value = TextEditingValue(
                    text: next.text,
                    selection: TextSelection.collapsed(offset: next.caret),
                  );
                },
              },
              child: TextField(
              controller: _body,
              focusNode: _bodyFocus,
              maxLines: null,
              minLines: 3,
              textCapitalization: TextCapitalization.sentences,
              style: const TextStyle(fontSize: 15, height: 1.45),
              decoration: InputDecoration(
                border: InputBorder.none,
                enabledBorder: InputBorder.none,
                focusedBorder: InputBorder.none,
                filled: false,
                hintText: 'Write something…',
                hintStyle: TextStyle(color: scheme.onSurfaceVariant),
                contentPadding: const EdgeInsets.symmetric(vertical: Gap.sm),
              ),
              onTapOutside: (_) {
                FocusScope.of(context).unfocus();
                if (_body.text != entry.body) {
                  _save(() => controller.save(body: _body.text));
                }
              },
              ),
            ),
            ),
          ),
        ),

        if (schemaFields.isNotEmpty) ...[
          const SizedBox(height: Gap.lg),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(Gap.lg),
              child: Column(
                children: [
                  for (final (i, field) in schemaFields.indexed) ...[
                    if (i > 0) const SizedBox(height: Gap.md),
                    DynamicFieldInput(
                      field: field,
                      value: _fieldValues[field.name],
                      onChanged: (v) {
                        setState(() => _fieldValues[field.name] = v);
                        _save(() => controller.save(fields: _fieldValues));
                      },
                    ),
                  ],
                ],
              ),
            ),
          ),
        ],

        // Formatting bar, shown while the body is being edited. Kept in the
        // tree with Visibility rather than an `if`: removing it mid-gesture
        // would cancel the tap that is being delivered to it.
        Visibility(
          visible: _bodyFocus.hasFocus,
          maintainState: true,
          child: _buildFormatBar(scheme),
        ),

        const SizedBox(height: Gap.lg),
        _AttachmentsSection(spaceId: widget.spaceId, entryId: widget.entryId),

        if (orphanFields.isNotEmpty) ...[
          const SizedBox(height: Gap.lg),
          Text(
            'OTHER DATA',
            style: TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w800,
              letterSpacing: 0.7,
              color: scheme.onSurfaceVariant,
            ),
          ),
          const SizedBox(height: Gap.sm),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(Gap.md),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  for (final key in orphanFields)
                    Padding(
                      padding: const EdgeInsets.symmetric(vertical: 3),
                      child: Text(
                        '$key: ${entry.fields[key]}',
                        style: TextStyle(fontSize: 13, color: scheme.onSurfaceVariant),
                      ),
                    ),
                ],
              ),
            ),
          ),
        ],

        if (entry.tags.isNotEmpty) ...[
          const SizedBox(height: Gap.lg),
          Wrap(
            spacing: Gap.sm,
            runSpacing: Gap.sm,
            children: [
              for (final tag in entry.tags)
                Chip(
                  label: Text(tag, style: const TextStyle(fontSize: 12.5)),
                  visualDensity: VisualDensity.compact,
                  backgroundColor: scheme.surfaceContainerHighest,
                  side: BorderSide.none,
                ),
            ],
          ),
        ],
      ],
    );
  }
}

/// Files attached to an entry: tap to open, long-press to remove.
///
/// The download endpoint is authenticated, so the bytes are fetched through Dio
/// (which carries the bearer token), written to a cache file, and handed to the
/// platform viewer — a plain URL would come back 401.
class _AttachmentsSection extends ConsumerStatefulWidget {
  const _AttachmentsSection({required this.spaceId, required this.entryId});

  final String spaceId;
  final String entryId;

  @override
  ConsumerState<_AttachmentsSection> createState() =>
      _AttachmentsSectionState();
}

class _AttachmentsSectionState extends ConsumerState<_AttachmentsSection> {
  /// Id of the attachment currently downloading, so its row can show progress
  /// and a second tap is ignored.
  String? _busyId;

  EntryRef get _ref => (spaceId: widget.spaceId, entryId: widget.entryId);

  Future<void> _open(SpaceAttachment attachment) async {
    if (_busyId != null) return;
    setState(() => _busyId = attachment.id);

    try {
      final bytes = await ref
          .read(spacesRepositoryProvider)
          .attachmentBytes(widget.spaceId, attachment.id);

      // Cache dir, not documents: this is a re-downloadable copy, and the OS is
      // free to reclaim it.
      final dir = await getTemporaryDirectory();
      final file = File('${dir.path}/${attachment.id}_${attachment.fileName}');
      await file.writeAsBytes(bytes);

      final result = await OpenFilex.open(
        file.path,
        type: attachment.contentType,
      );
      if (result.type != ResultType.done && mounted) {
        showAppSnack(context, 'No app can open ${attachment.fileName}.',
            error: true);
      }
    } on ApiException catch (e) {
      if (mounted) showAppSnack(context, e.message, error: true);
    } catch (_) {
      if (mounted) {
        showAppSnack(context, 'Could not open that attachment.', error: true);
      }
    } finally {
      if (mounted) setState(() => _busyId = null);
    }
  }

  Future<void> _delete(SpaceAttachment attachment) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Remove attachment?'),
        content: Text('${attachment.fileName} will be deleted.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Remove'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;

    try {
      await ref
          .read(spacesRepositoryProvider)
          .deleteAttachment(widget.spaceId, attachment.id);
      ref.invalidate(entryAttachmentsProvider(_ref));
    } on ApiException catch (e) {
      if (mounted) showAppSnack(context, e.message, error: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final async = ref.watch(entryAttachmentsProvider(_ref));
    final attachments = async.value ?? const <SpaceAttachment>[];

    // Nothing to show and nothing loading: stay out of the way.
    if (attachments.isEmpty && !async.isLoading) return const SizedBox.shrink();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'ATTACHMENTS',
          style: TextStyle(
            fontSize: 11,
            fontWeight: FontWeight.w800,
            letterSpacing: 0.7,
            color: scheme.onSurfaceVariant,
          ),
        ),
        const SizedBox(height: Gap.sm),
        Card(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              for (final a in attachments)
                ListTile(
                  dense: true,
                  leading: _busyId == a.id
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : Icon(_iconFor(a.contentType), size: 22),
                  title: Text(
                    a.fileName,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontSize: 14),
                  ),
                  subtitle: Text(
                    a.readableSize,
                    style: TextStyle(
                      fontSize: 12,
                      color: scheme.onSurfaceVariant,
                    ),
                  ),
                  onTap: () => _open(a),
                  onLongPress: () => _delete(a),
                ),
            ],
          ),
        ),
      ],
    );
  }

  IconData _iconFor(String contentType) {
    if (contentType.startsWith('image/')) return Icons.image_outlined;
    if (contentType.contains('pdf')) return Icons.picture_as_pdf_outlined;
    return Icons.insert_drive_file_outlined;
  }
}
