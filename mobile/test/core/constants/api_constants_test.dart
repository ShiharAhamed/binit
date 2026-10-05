import 'dart:io';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/constants/api_constants.dart';

void main() {
  group('ApiConstants Tests', () {
    test('resolves default baseUrl correctly for current platform', () {
      final url = ApiConstants.baseUrl;
      if (Platform.isAndroid) {
        expect(url, 'http://10.0.2.2:5276/api/v1');
      } else {
        expect(url, 'http://localhost:5276/api/v1');
      }
    });

    test('endpoint paths match backend route specifications', () {
      expect(ApiConstants.register, '/auth/register');
      expect(ApiConstants.login, '/auth/login');
      expect(ApiConstants.me, '/auth/me');
      expect(ApiConstants.changePassword, '/auth/change-password');
      expect(ApiConstants.driverProfile('drv-1'), '/drivers/drv-1');
      expect(ApiConstants.driverAvailability, '/drivers/me/availability');
      expect(ApiConstants.myAssignments, '/assignments/mine');
      expect(ApiConstants.assignmentDetail('asg-1'), '/assignments/asg-1');
      expect(ApiConstants.routeDetail('rt-1'), '/routes/rt-1');
    });

    test('network timeouts are set to 15 seconds', () {
      expect(ApiConstants.connectTimeout, const Duration(seconds: 15));
      expect(ApiConstants.receiveTimeout, const Duration(seconds: 15));
    });
  });
}
