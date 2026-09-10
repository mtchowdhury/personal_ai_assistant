import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:cmdnext_mobile/features/tasks/data/tasks_controller.dart';

void main() {
  test('calendar month navigation never produces an out-of-range month', () {
    final container = ProviderContainer();
    addTearDown(container.dispose);

    final notifier = container.read(calendarMonthProvider.notifier);

    // Walk forward 36 months and back 60, asserting month is always 1-12
    // after every single transition - this is exactly what rapid tapping
    // on next/previous would do.
    for (var i = 0; i < 36; i++) {
      notifier.next();
      final m = container.read(calendarMonthProvider);
      expect(m.month, inInclusiveRange(1, 12), reason: 'after next() #$i');
    }
    for (var i = 0; i < 60; i++) {
      notifier.previous();
      final m = container.read(calendarMonthProvider);
      expect(m.month, inInclusiveRange(1, 12), reason: 'after previous() #$i');
    }
  });
}
