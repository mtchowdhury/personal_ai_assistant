/// The signed-in user.
///
/// Note the casing: `AuthController` builds its response from an anonymous
/// object with **lowercase** property names (`token`, `user`, `expiresIn`,
/// `firstName`), so these are read as-is rather than camelCased by convention.
class AppUser {
  const AppUser({
    required this.id,
    required this.email,
    this.firstName,
    this.lastName,
  });

  final String id;
  final String email;
  final String? firstName;
  final String? lastName;

  /// A short label for the app bar — first name, else the local part of the
  /// email, so there is always something to greet.
  String get shortName {
    final f = firstName?.trim();
    if (f != null && f.isNotEmpty) return f;
    final at = email.indexOf('@');
    return at > 0 ? email.substring(0, at) : email;
  }

  String get initials {
    final f = (firstName ?? '').trim();
    final l = (lastName ?? '').trim();
    if (f.isNotEmpty && l.isNotEmpty) {
      return '${f[0]}${l[0]}'.toUpperCase();
    }
    if (f.isNotEmpty) return f[0].toUpperCase();
    return email.isNotEmpty ? email[0].toUpperCase() : '?';
  }

  factory AppUser.fromJson(Map<String, dynamic> json) => AppUser(
    id: (json['id'] ?? '').toString(),
    email: (json['email'] ?? '').toString(),
    firstName: json['firstName'] as String?,
    lastName: json['lastName'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'email': email,
    'firstName': firstName,
    'lastName': lastName,
  };
}

/// The login/register payload: `{ token, user, expiresIn }`.
class AuthResult {
  const AuthResult({required this.token, required this.user});

  final String token;
  final AppUser user;

  factory AuthResult.fromJson(Map<String, dynamic> json) => AuthResult(
    token: (json['token'] ?? '').toString(),
    user: AppUser.fromJson(
      (json['user'] as Map?)?.cast<String, dynamic>() ?? const {},
    ),
  );
}
