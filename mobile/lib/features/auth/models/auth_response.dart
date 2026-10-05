import 'auth_user.dart';

/// Response payload returned by POST /auth/register and POST /auth/login.
class AuthResponse {
  final String accessToken;
  final DateTime expiresAt;
  final AuthUser user;
  final bool mustChangePassword;

  const AuthResponse({
    required this.accessToken,
    required this.expiresAt,
    required this.user,
    this.mustChangePassword = false,
  });

  factory AuthResponse.fromJson(Map<String, dynamic> json) {
    final userJson = json['user'] as Map<String, dynamic>? ?? <String, dynamic>{};
    final bool mcp = (json['mustChangePassword'] as bool?) ??
        (userJson['mustChangePassword'] as bool?) ??
        false;

    return AuthResponse(
      accessToken: json['accessToken'] as String? ?? '',
      expiresAt: json['expiresAt'] != null
          ? DateTime.parse(json['expiresAt'].toString())
          : DateTime.now().add(const Duration(hours: 1)),
      user: AuthUser.fromJson({
        ...userJson,
        'mustChangePassword': mcp,
      }),
      mustChangePassword: mcp,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'accessToken': accessToken,
      'expiresAt': expiresAt.toIso8601String(),
      'user': user.toJson(),
      'mustChangePassword': mustChangePassword,
    };
  }
}
