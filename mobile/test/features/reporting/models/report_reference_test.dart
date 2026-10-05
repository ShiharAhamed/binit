import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/reporting/models/report_reference.dart';

void main() {
  test('uses the backend reference for display while retaining the full ID elsewhere', () {
    const id = 'c17add4f-79f3-4a0a-a9e0-150fde7d7827';
    expect(reportLabel(id, authoritative: 'C17ADD4F'), 'Report C17ADD4F');
    expect(reportReference(id), 'C17ADD4F');
    expect(reportLabel(id), 'Report C17ADD4F');
    expect(reportLabel(id).contains(id), isFalse);
  });
}
