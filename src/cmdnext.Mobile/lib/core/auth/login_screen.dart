import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_exception.dart';
import '../storage/app_storage.dart';
import '../theme/app_theme.dart';
import '../widgets/app_states.dart';
import 'auth_controller.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _email = TextEditingController();
  final _password = TextEditingController();
  final _passwordFocus = FocusNode();

  bool _obscure = true;
  bool _registering = false;
  final _firstName = TextEditingController();
  String? _error;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    _firstName.dispose();
    _passwordFocus.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    setState(() => _error = null);
    if (!(_formKey.currentState?.validate() ?? false)) return;
    FocusScope.of(context).unfocus();

    final auth = ref.read(authProvider.notifier);
    try {
      if (_registering) {
        await auth.register(
          email: _email.text,
          password: _password.text,
          firstName: _firstName.text.isEmpty ? null : _firstName.text,
        );
      } else {
        await auth.signIn(email: _email.text, password: _password.text);
      }
      // Routing is driven by auth state, so there is nothing to pop here.
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    }
  }

  @override
  Widget build(BuildContext context) {
    final signingIn = ref.watch(authProvider).signingIn;
    final scheme = Theme.of(context).colorScheme;

    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(
              horizontal: Gap.xl,
              vertical: Gap.xxl,
            ),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const _Wordmark(),
                    const SizedBox(height: Gap.xxl),
                    Text(
                      _registering ? 'Create your account' : 'Welcome back',
                      style: Theme.of(context).textTheme.headlineSmall
                          ?.copyWith(
                            fontWeight: FontWeight.w700,
                            letterSpacing: -0.5,
                          ),
                    ),
                    const SizedBox(height: Gap.xs),
                    Text(
                      _registering
                          ? 'One account, all your spaces.'
                          : 'Sign in to pick up where you left off.',
                      style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: scheme.onSurfaceVariant,
                      ),
                    ),
                    const SizedBox(height: Gap.xl),

                    if (_registering) ...[
                      TextFormField(
                        controller: _firstName,
                        textCapitalization: TextCapitalization.words,
                        textInputAction: TextInputAction.next,
                        decoration: const InputDecoration(
                          labelText: 'First name',
                          prefixIcon: Icon(Icons.person_outline_rounded),
                        ),
                      ),
                      const SizedBox(height: Gap.md),
                    ],

                    TextFormField(
                      controller: _email,
                      keyboardType: TextInputType.emailAddress,
                      autocorrect: false,
                      textInputAction: TextInputAction.next,
                      autofillHints: const [AutofillHints.username],
                      decoration: const InputDecoration(
                        labelText: 'Email',
                        prefixIcon: Icon(Icons.alternate_email_rounded),
                      ),
                      onFieldSubmitted: (_) => _passwordFocus.requestFocus(),
                      validator: (v) {
                        final t = (v ?? '').trim();
                        if (t.isEmpty) return 'Enter your email';
                        if (!t.contains('@') || !t.contains('.')) {
                          return 'That does not look like an email';
                        }
                        return null;
                      },
                    ),
                    const SizedBox(height: Gap.md),

                    TextFormField(
                      controller: _password,
                      focusNode: _passwordFocus,
                      obscureText: _obscure,
                      textInputAction: TextInputAction.go,
                      autofillHints: const [AutofillHints.password],
                      decoration: InputDecoration(
                        labelText: 'Password',
                        prefixIcon: const Icon(Icons.lock_outline_rounded),
                        suffixIcon: IconButton(
                          // Scoped to focus-visible equivalents by default on
                          // iOS; a plain tap here should not leave a ring.
                          onPressed: () =>
                              setState(() => _obscure = !_obscure),
                          icon: Icon(
                            _obscure
                                ? Icons.visibility_outlined
                                : Icons.visibility_off_outlined,
                          ),
                          tooltip: _obscure ? 'Show password' : 'Hide password',
                        ),
                      ),
                      onFieldSubmitted: (_) => _submit(),
                      validator: (v) {
                        if ((v ?? '').isEmpty) return 'Enter your password';
                        if (_registering && (v ?? '').length < 8) {
                          return 'Use at least 8 characters';
                        }
                        return null;
                      },
                    ),

                    if (_error != null) ...[
                      const SizedBox(height: Gap.lg),
                      _ErrorBanner(message: _error!),
                    ],

                    const SizedBox(height: Gap.xl),
                    FilledButton(
                      onPressed: signingIn ? null : _submit,
                      child: signingIn
                          ? const SizedBox(
                              height: 20,
                              width: 20,
                              child: CircularProgressIndicator(
                                strokeWidth: 2.2,
                                color: Colors.white,
                              ),
                            )
                          : Text(_registering ? 'Create account' : 'Sign in'),
                    ),
                    const SizedBox(height: Gap.md),
                    TextButton(
                      onPressed: signingIn
                          ? null
                          : () => setState(() {
                              _registering = !_registering;
                              _error = null;
                            }),
                      child: Text(
                        _registering
                            ? 'I already have an account'
                            : 'Create an account',
                      ),
                    ),

                    const SizedBox(height: Gap.lg),
                    const Divider(),
                    const _ServerRow(),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _Wordmark extends StatelessWidget {
  const _Wordmark();

  @override
  Widget build(BuildContext context) => Row(
    mainAxisAlignment: MainAxisAlignment.center,
    children: [
      Container(
        width: 44,
        height: 44,
        decoration: BoxDecoration(
          color: AppColors.accent,
          borderRadius: BorderRadius.circular(12),
        ),
        alignment: Alignment.center,
        child: const Text(
          '>_',
          style: TextStyle(
            color: Colors.white,
            fontWeight: FontWeight.w800,
            fontSize: 17,
            height: 1,
          ),
        ),
      ),
      const SizedBox(width: Gap.md),
      const Text(
        'cmdnext',
        style: TextStyle(
          fontSize: 26,
          fontWeight: FontWeight.w800,
          letterSpacing: -0.8,
        ),
      ),
    ],
  );
}

class _ErrorBanner extends StatelessWidget {
  const _ErrorBanner({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.all(Gap.md),
    decoration: BoxDecoration(
      color: AppColors.red.withValues(alpha: 0.1),
      borderRadius: BorderRadius.circular(Radii.card),
      border: Border.all(color: AppColors.red.withValues(alpha: 0.35)),
    ),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Icon(Icons.error_outline_rounded, size: 18, color: AppColors.red),
        const SizedBox(width: Gap.sm),
        Expanded(
          child: Text(
            message,
            style: const TextStyle(color: AppColors.red, fontSize: 13.5),
          ),
        ),
      ],
    ),
  );
}

/// The server is on a private private network with no public name, so it must be
/// changeable without a rebuild — a device rename or a local API would
/// otherwise lock the app out.
class _ServerRow extends ConsumerWidget {
  const _ServerRow();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final url = ref.watch(serverUrlProvider);
    final scheme = Theme.of(context).colorScheme;

    return InkWell(
      onTap: () => _editServer(context, ref, url),
      borderRadius: BorderRadius.circular(Radii.card),
      child: Padding(
        padding: const EdgeInsets.symmetric(
          vertical: Gap.md,
          horizontal: Gap.sm,
        ),
        child: Row(
          children: [
            Icon(
              Icons.dns_outlined,
              size: 17,
              color: scheme.onSurfaceVariant,
            ),
            const SizedBox(width: Gap.sm),
            Text(
              'Server',
              style: TextStyle(
                fontSize: 13,
                color: scheme.onSurfaceVariant,
                fontWeight: FontWeight.w500,
              ),
            ),
            const Spacer(),
            Flexible(
              child: Text(
                url.replaceFirst(RegExp(r'^https?://'), ''),
                textAlign: TextAlign.end,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                  fontFamily: 'Menlo',
                ),
              ),
            ),
            const SizedBox(width: Gap.xs),
            Icon(
              Icons.chevron_right_rounded,
              size: 18,
              color: scheme.onSurfaceVariant,
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _editServer(
    BuildContext context,
    WidgetRef ref,
    String current,
  ) async {
    final controller = TextEditingController(text: current);
    final saved = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      builder: (sheetContext) => Padding(
        padding: EdgeInsets.only(
          left: Gap.xl,
          right: Gap.xl,
          top: Gap.sm,
          bottom: MediaQuery.viewInsetsOf(sheetContext).bottom + Gap.xl,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              'Server address',
              style: Theme.of(sheetContext).textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: Gap.xs),
            Text(
              'cmdnext is reachable over the private network only. The default is the '
              'hostname, which survives an IP change.',
              style: Theme.of(sheetContext).textTheme.bodySmall,
            ),
            const SizedBox(height: Gap.lg),
            TextField(
              controller: controller,
              autofocus: true,
              keyboardType: TextInputType.url,
              autocorrect: false,
              inputFormatters: [FilteringTextInputFormatter.deny(RegExp(r'\s'))],
              decoration: const InputDecoration(
                prefixIcon: Icon(Icons.link_rounded),
                hintText: kDefaultServerUrl,
              ),
              onSubmitted: (v) => Navigator.of(sheetContext).pop(v),
            ),
            const SizedBox(height: Gap.lg),
            FilledButton(
              onPressed: () =>
                  Navigator.of(sheetContext).pop(controller.text),
              child: const Text('Save'),
            ),
            TextButton(
              onPressed: () =>
                  Navigator.of(sheetContext).pop(kDefaultServerUrl),
              child: const Text('Reset to default'),
            ),
          ],
        ),
      ),
    );
    controller.dispose();

    if (saved == null) return;
    await ref.read(serverUrlProvider.notifier).set(saved);
    if (context.mounted) {
      showAppSnack(context, 'Server set to ${ref.read(serverUrlProvider)}');
    }
  }
}
