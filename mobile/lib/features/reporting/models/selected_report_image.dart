/// Value object representing an image selected by the Citizen as photo evidence.
/// Kept provider-independent so presentation logic and API request preparation
/// are decoupled from device-specific picker implementations (such as XFile).
class SelectedReportImage {
  final String path;
  final String fileName;
  final String fileType; // Canonical MIME type, e.g. 'image/jpeg', 'image/png', 'image/webp'
  final int sizeBytes;

  const SelectedReportImage({
    required this.path,
    required this.fileName,
    required this.fileType,
    required this.sizeBytes,
  });

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is SelectedReportImage &&
          runtimeType == other.runtimeType &&
          path == other.path;

  @override
  int get hashCode => path.hashCode;

  @override
  String toString() =>
      'SelectedReportImage(path: $path, fileName: $fileName, type: $fileType, size: $sizeBytes bytes)';
}
