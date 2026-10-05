/// Domain roles matching ASP.NET Core Identity roles exactly.
class AppRoles {
  AppRoles._();

  static const String citizen = 'Citizen';
  static const String wasteOfficer = 'WasteOfficer';
  static const String driver = 'Driver';
  static const String municipalManager = 'MunicipalManager';

  static const List<String> all = [
    citizen,
    wasteOfficer,
    driver,
    municipalManager,
  ];
}

/// Represents an authenticated user in the mobile application.
class AuthUser {
  final String id;
  final String fullName;
  final String email;
  final String role;
  final String? phoneNumber;
  final bool? isActive;
  final DateTime? createdAt;
  final bool mustChangePassword;

  const AuthUser({
    required this.id,
    required this.fullName,
    required this.email,
    required this.role,
    this.phoneNumber,
    this.isActive,
    this.createdAt,
    this.mustChangePassword = false,
  });

  factory AuthUser.fromJson(Map<String, dynamic> json) {
    return AuthUser(
      id: json['id'] as String? ?? '',
      fullName: json['fullName'] as String? ?? '',
      email: json['email'] as String? ?? '',
      role: json['role'] as String? ?? AppRoles.citizen,
      phoneNumber: json['phoneNumber'] as String?,
      isActive: json['isActive'] as bool?,
      createdAt: json['createdAt'] != null
          ? DateTime.tryParse(json['createdAt'].toString())
          : null,
      mustChangePassword: json['mustChangePassword'] as bool? ?? false,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'fullName': fullName,
      'email': email,
      'role': role,
      'mustChangePassword': mustChangePassword,
      if (phoneNumber != null) 'phoneNumber': phoneNumber,
      if (isActive != null) 'isActive': isActive,
      if (createdAt != null) 'createdAt': createdAt!.toIso8601String(),
    };
  }

  bool get isCitizen => role == AppRoles.citizen;
  bool get isWasteOfficer => role == AppRoles.wasteOfficer;
  bool get isDriver => role == AppRoles.driver;
  bool get isManager => role == AppRoles.municipalManager;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is AuthUser &&
          runtimeType == other.runtimeType &&
          id == other.id &&
          email == other.email &&
          role == other.role;

  @override
  int get hashCode => id.hashCode ^ email.hashCode ^ role.hashCode;
}
