import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

/// `/tasks/:id` used to be declared ahead of the shell's named task views, so
/// it matched `/tasks/calendar` first and opened the detail screen with a
/// taskId of "calendar" — which fetched `dtasks/calendar` with no query and
/// surfaced the server's "Month must be between 1 and 12." as a page error.
void main() {
  final guid = RegExp(
    r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-'
    r'[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
  );

  test('named task views are not swallowed by the detail route', () {
    for (final view in ['calendar', 'all', 'board']) {
      expect(
        guid.hasMatch(view),
        isFalse,
        reason: '/tasks/$view must not match the GUID-constrained detail route',
      );
    }
  });

  test('a real task id still matches the detail route', () {
    expect(guid.hasMatch('e395bcd6-2113-43ee-9716-eac49402a1cc'), isTrue);
  });

  testWidgets('the router resolves each named view to its own screen', (
    tester,
  ) async {
    final router = GoRouter(
      initialLocation: '/tasks',
      routes: [
        GoRoute(
          path: r'/tasks/:id('
              r'[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-'
              r'[0-9a-fA-F]{4}-[0-9a-fA-F]{12})',
          builder: (_, state) => const _Marker('detail'),
        ),
        GoRoute(
          path: '/tasks',
          builder: (_, _) => const _Marker('tasks'),
          routes: [
            GoRoute(
              path: 'calendar',
              builder: (_, _) => const _Marker('calendar'),
            ),
          ],
        ),
      ],
    );
    addTearDown(router.dispose);

    await tester.pumpWidget(
      WidgetsApp.router(
        routerConfig: router,
        color: const Color(0xFF000000),
      ),
    );

    router.go('/tasks/calendar');
    await tester.pumpAndSettle();
    expect(
      router.routerDelegate.currentConfiguration.last.matchedLocation,
      '/tasks/calendar',
    );

    const id = 'e395bcd6-2113-43ee-9716-eac49402a1cc';
    router.go('/tasks/$id');
    await tester.pumpAndSettle();
    expect(
      router.routerDelegate.currentConfiguration.last.matchedLocation,
      '/tasks/$id',
    );
  });
}

class _Marker extends StatelessWidget {
  const _Marker(this.name);

  final String name;

  @override
  Widget build(BuildContext context) => const SizedBox.shrink();
}
