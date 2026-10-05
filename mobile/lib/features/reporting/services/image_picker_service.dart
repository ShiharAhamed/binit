import 'dart:async';
import 'dart:math';
import 'package:flutter/services.dart';
import 'package:image_picker/image_picker.dart';
import '../models/selected_report_image.dart';

/// User-facing exception thrown when image selection or validation fails.
class ImagePickerException implements Exception {
  final String message;

  const ImagePickerException(this.message);

  @override
  String toString() => message;
}

/// Validates file size and magic-byte signatures for citizen photo evidence.
/// Mirrors backend SmartWaste attachment validation rules:
/// - Maximum 5 MB (5,242,880 bytes) per image
/// - Magic-byte detection for JPEG, PNG, and WebP
class ImageValidator {
  static const int maxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

  // Magic bytes matching backend ImageSignatureValidator
  static const List<int> jpegMagic = [0xFF, 0xD8, 0xFF];
  static const List<int> pngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
  static const List<int> riffHeader = [0x52, 0x49, 0x46, 0x46]; // "RIFF"
  static const List<int> webpHeader = [0x57, 0x45, 0x42, 0x50]; // "WEBP"

  /// Detects the canonical MIME type based on header bytes.
  /// Returns 'image/jpeg', 'image/png', 'image/webp', or null if unsupported.
  static String? detectMimeType(List<int> bytes) {
    if (bytes.length < 3) return null;

    // JPEG check: first 3 bytes are FF D8 FF
    if (bytes.length >= 3 &&
        bytes[0] == jpegMagic[0] &&
        bytes[1] == jpegMagic[1] &&
        bytes[2] == jpegMagic[2]) {
      return 'image/jpeg';
    }

    // PNG check: first 8 bytes
    if (bytes.length >= 8 &&
        bytes[0] == pngMagic[0] &&
        bytes[1] == pngMagic[1] &&
        bytes[2] == pngMagic[2] &&
        bytes[3] == pngMagic[3] &&
        bytes[4] == pngMagic[4] &&
        bytes[5] == pngMagic[5] &&
        bytes[6] == pngMagic[6] &&
        bytes[7] == pngMagic[7]) {
      return 'image/png';
    }

    // WebP check: bytes 0..3 are "RIFF" and bytes 8..11 are "WEBP"
    if (bytes.length >= 12 &&
        bytes[0] == riffHeader[0] &&
        bytes[1] == riffHeader[1] &&
        bytes[2] == riffHeader[2] &&
        bytes[3] == riffHeader[3] &&
        bytes[8] == webpHeader[0] &&
        bytes[9] == webpHeader[1] &&
        bytes[10] == webpHeader[2] &&
        bytes[11] == webpHeader[3]) {
      return 'image/webp';
    }

    return null;
  }

  /// Validates an [XFile] efficiently by checking length first,
  /// then reading only the first 16 bytes for magic-byte signature validation.
  static Future<SelectedReportImage> validateXFile(XFile file) async {
    final length = await file.length();

    // 1. File size check (max 5 MB)
    if (length > maxFileSizeBytes) {
      throw const ImagePickerException('Each photo must be 5 MB or smaller.');
    }

    // 2. Minimum length check (shortest supported header is 3 bytes for JPEG)
    if (length < 3) {
      throw const ImagePickerException('Only JPEG, PNG, or WebP photos are supported.');
    }

    // 3. Read only enough initial bytes (16 bytes) to identify format
    List<int> headerBytes;
    try {
      final stream = file.openRead(0, min(16, length));
      final chunks = <int>[];
      await for (final chunk in stream) {
        chunks.addAll(chunk);
        if (chunks.length >= 16) break;
      }
      headerBytes = chunks;
    } catch (_) {
      // Fallback in environments where openRead slice is unsupported
      final allBytes = await file.readAsBytes();
      headerBytes = allBytes.sublist(0, min(16, allBytes.length));
    }

    // 4. Magic-byte signature detection
    final mime = detectMimeType(headerBytes);
    if (mime == null) {
      throw const ImagePickerException('Only JPEG, PNG, or WebP photos are supported.');
    }

    final cleanName = file.name.split(RegExp(r'[/\\]')).last;

    return SelectedReportImage(
      path: file.path,
      fileName: cleanName.isNotEmpty ? cleanName : file.name,
      fileType: mime,
      sizeBytes: length,
    );
  }
}

/// Abstract contract for camera and photo gallery picking.
/// Allows unit and widget tests to inject simulated pickers without invoking
/// native platform channels or launching real camera hardware.
abstract class ImagePickerService {
  /// Captures a single photo from device camera.
  /// Returns null if the user cancelled the capture.
  Future<SelectedReportImage?> takePhoto();

  /// Picks one or more photos from the device gallery up to [maxImages].
  /// Returns empty list if cancelled.
  Future<List<SelectedReportImage>> pickFromGallery({
    required int maxImages,
  });

  /// Recovers lost images from Android Activity termination.
  Future<List<SelectedReportImage>> retrieveLostImages();
}

/// Production implementation of [ImagePickerService] wrapping Flutter's [ImagePicker].
class DefaultImagePickerService implements ImagePickerService {
  final ImagePicker _picker;

  DefaultImagePickerService({ImagePicker? picker}) : _picker = picker ?? ImagePicker();

  @override
  Future<SelectedReportImage?> takePhoto() async {
    try {
      final xfile = await _picker.pickImage(source: ImageSource.camera);
      if (xfile == null) return null;
      return await ImageValidator.validateXFile(xfile);
    } on ImagePickerException {
      rethrow;
    } on PlatformException catch (_) {
      throw const ImagePickerException('Camera is not available or permission was denied.');
    } catch (_) {
      throw const ImagePickerException('Failed to capture photo. Please try again.');
    }
  }

  @override
  Future<List<SelectedReportImage>> pickFromGallery({
    required int maxImages,
  }) async {
    if (maxImages <= 0) return const [];

    try {
      final xfiles = await _picker.pickMultiImage(limit: maxImages);
      if (xfiles.isEmpty) return const [];

      final results = <SelectedReportImage>[];
      for (final xfile in xfiles.take(maxImages)) {
        final validated = await ImageValidator.validateXFile(xfile);
        results.add(validated);
      }
      return results;
    } on ImagePickerException {
      rethrow;
    } on PlatformException catch (_) {
      throw const ImagePickerException('Unable to access photo gallery.');
    } catch (_) {
      throw const ImagePickerException('Failed to select photos. Please try again.');
    }
  }

  @override
  Future<List<SelectedReportImage>> retrieveLostImages() async {
    try {
      final response = await _picker.retrieveLostData();
      if (response.isEmpty || response.exception != null) {
        return const [];
      }

      final rawFiles = response.files ?? [if (response.file != null) response.file!];
      final results = <SelectedReportImage>[];
      for (final xfile in rawFiles.take(3)) {
        try {
          final validated = await ImageValidator.validateXFile(xfile);
          results.add(validated);
        } catch (_) {
          // Ignore invalid or corrupted files from lost data
        }
      }
      return results;
    } catch (_) {
      return const [];
    }
  }
}
