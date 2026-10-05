/// Photographic attachment associated with a waste report.
/// Exposes the short-lived signed FileUrl for client rendering.
/// Internal StorageKey is server-only and not included.
class ReportAttachmentModel {
  final String id;
  final String wasteReportId;
  final String fileUrl;
  final String fileType;
  final DateTime createdAt;

  const ReportAttachmentModel({
    required this.id,
    required this.wasteReportId,
    required this.fileUrl,
    required this.fileType,
    required this.createdAt,
  });

  factory ReportAttachmentModel.fromJson(Map<String, dynamic> json) {
    final id = json['id'];
    if (id == null || id is! String || id.trim().isEmpty) {
      throw const FormatException("Missing or invalid required field 'id'");
    }
    final wasteReportId = json['wasteReportId'];
    if (wasteReportId == null || wasteReportId is! String || wasteReportId.trim().isEmpty) {
      throw const FormatException("Missing or invalid required field 'wasteReportId'");
    }
    final fileUrl = json['fileUrl'];
    if (fileUrl == null || fileUrl is! String || fileUrl.trim().isEmpty) {
      throw const FormatException("Missing or invalid required field 'fileUrl'");
    }
    final fileType = json['fileType'];
    if (fileType == null || fileType is! String || fileType.trim().isEmpty) {
      throw const FormatException("Missing or invalid required field 'fileType'");
    }
    final createdAtRaw = json['createdAt'];
    if (createdAtRaw == null) {
      throw const FormatException("Missing required field 'createdAt'");
    }
    final createdAt = DateTime.tryParse(createdAtRaw.toString());
    if (createdAt == null) {
      throw FormatException("Invalid DateTime format for 'createdAt': '$createdAtRaw'");
    }

    return ReportAttachmentModel(
      id: id,
      wasteReportId: wasteReportId,
      fileUrl: fileUrl,
      fileType: fileType,
      createdAt: createdAt.toUtc(),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'wasteReportId': wasteReportId,
      'fileUrl': fileUrl,
      'fileType': fileType,
      'createdAt': createdAt.toIso8601String(),
    };
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is ReportAttachmentModel &&
          runtimeType == other.runtimeType &&
          id == other.id &&
          wasteReportId == other.wasteReportId &&
          fileUrl == other.fileUrl &&
          fileType == other.fileType &&
          createdAt == other.createdAt;

  @override
  int get hashCode =>
      id.hashCode ^
      wasteReportId.hashCode ^
      fileUrl.hashCode ^
      fileType.hashCode ^
      createdAt.hashCode;
}
