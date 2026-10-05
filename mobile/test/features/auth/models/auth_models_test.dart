import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/auth/models/auth_response.dart';
import 'package:mobile/features/auth/models/auth_user.dart';

void main() {
  group('AuthUser Model Tests', () {
    test('deserializes JSON accurately', () {
      final json = {
        'id': 'user-12345',
        'fullName': 'Kasun Perera',
        'email': 'kasun@binit.lk',
        'phoneNumber': '+94712345678',
        'role': 'Citizen',
      };

      final user = AuthUser.fromJson(json);

      expect(user.id, 'user-12345');
      expect(user.fullName, 'Kasun Perera');
      expect(user.email, 'kasun@binit.lk');
      expect(user.phoneNumber, '+94712345678');
      expect(user.role, 'Citizen');
      expect(user.isCitizen, isTrue);
    });

    test('serializes to JSON correctly', () {
      const user = AuthUser(
        id: 'user-123',
        fullName: 'Nimal Silva',
        email: 'nimal@binit.lk',
        role: 'Citizen',
      );

      final json = user.toJson();

      expect(json['id'], 'user-123');
      expect(json['fullName'], 'Nimal Silva');
      expect(json['email'], 'nimal@binit.lk');
      expect(json['role'], 'Citizen');
      expect(json['phoneNumber'], isNull);
    });

    test('role helper properties report correctly', () {
      const citizen = AuthUser(id: '1', fullName: 'A', email: 'a@a.com', role: AppRoles.citizen);
      const officer = AuthUser(id: '2', fullName: 'B', email: 'b@b.com', role: AppRoles.wasteOfficer);
      const driver = AuthUser(id: '3', fullName: 'C', email: 'c@c.com', role: AppRoles.driver);
      const manager = AuthUser(id: '4', fullName: 'D', email: 'd@d.com', role: AppRoles.municipalManager);

      expect(citizen.isCitizen, isTrue);
      expect(officer.isWasteOfficer, isTrue);
      expect(driver.isDriver, isTrue);
      expect(manager.isManager, isTrue);
    });
  });

  group('AuthResponse Model Tests', () {
    test('deserializes complete auth response JSON', () {
      final json = {
        'accessToken': 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...',
        'expiresAt': '2026-09-07T12:00:00.000Z',
        'user': {
          'id': 'user-999',
          'fullName': 'Saman Kumara',
          'email': 'saman@binit.lk',
          'role': 'Citizen',
        },
      };

      final response = AuthResponse.fromJson(json);

      expect(response.accessToken, 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...');
      expect(response.expiresAt, DateTime.parse('2026-09-07T12:00:00.000Z'));
      expect(response.user.id, 'user-999');
      expect(response.user.fullName, 'Saman Kumara');
      expect(response.user.isCitizen, isTrue);
    });

    test('serializes to JSON correctly', () {
      final expiry = DateTime.parse('2026-09-07T12:00:00.000Z');
      final response = AuthResponse(
        accessToken: 'token-abc',
        expiresAt: expiry,
        user: const AuthUser(
          id: 'u-1',
          fullName: 'Test User',
          email: 'test@binit.lk',
          role: 'Citizen',
          mustChangePassword: true,
        ),
        mustChangePassword: true,
      );

      final json = response.toJson();
      expect(json['accessToken'], 'token-abc');
      expect(json['expiresAt'], '2026-09-07T12:00:00.000Z');
      expect(json['user']['fullName'], 'Test User');
      expect(json['user']['mustChangePassword'], isTrue);
      expect(json['mustChangePassword'], isTrue);
    });

    test('deserializes mustChangePassword correctly from AuthResponse', () {
      final json = {
        'accessToken': 'token-xyz',
        'expiresAt': '2026-09-07T12:00:00.000Z',
        'mustChangePassword': true,
        'user': {
          'id': 'u-2',
          'fullName': 'Driver User',
          'email': 'driver@binit.lk',
          'role': 'Driver',
        },
      };

      final response = AuthResponse.fromJson(json);
      expect(response.mustChangePassword, isTrue);
      expect(response.user.mustChangePassword, isTrue);
      expect(response.user.isDriver, isTrue);
    });
  });
}
