import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:image_picker/image_picker.dart';
import 'package:mobile/features/reporting/models/selected_report_image.dart';
import 'package:mobile/features/reporting/services/image_picker_service.dart';

void main() {
  group('SelectedReportImage Model Tests', () {
    test('equality and hashCode are based on path', () {
      const img1 = SelectedReportImage(
        path: '/tmp/photo1.jpg',
        fileName: 'photo1.jpg',
        fileType: 'image/jpeg',
        sizeBytes: 1024,
      );
      const img2 = SelectedReportImage(
        path: '/tmp/photo1.jpg',
        fileName: 'different_name.jpg',
        fileType: 'image/jpeg',
        sizeBytes: 2048,
      );
      const img3 = SelectedReportImage(
        path: '/tmp/photo2.jpg',
        fileName: 'photo2.jpg',
        fileType: 'image/jpeg',
        sizeBytes: 1024,
      );

      expect(img1, equals(img2));
      expect(img1.hashCode, equals(img2.hashCode));
      expect(img1, isNot(equals(img3)));
      expect(img1.toString(), contains('/tmp/photo1.jpg'));
    });
  });

  group('ImageValidator.detectMimeType Tests', () {
    test('identifies JPEG magic bytes FF D8 FF', () {
      final bytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
      expect(ImageValidator.detectMimeType(bytes), equals('image/jpeg'));
    });

    test('identifies PNG magic bytes 89 50 4E 47 0D 0A 1A 0A', () {
      final bytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
      expect(ImageValidator.detectMimeType(bytes), equals('image/png'));
    });

    test('identifies WebP magic bytes RIFF....WEBP', () {
      final bytes = [
        0x52, 0x49, 0x46, 0x46, // "RIFF"
        0x20, 0x00, 0x00, 0x00, // size
        0x57, 0x45, 0x42, 0x50, // "WEBP"
        0x56, 0x50, 0x38, 0x20, // "VP8 "
      ];
      expect(ImageValidator.detectMimeType(bytes), equals('image/webp'));
    });

    test('rejects GIF89a format', () {
      final bytes = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00];
      expect(ImageValidator.detectMimeType(bytes), isNull);
    });

    test('rejects PDF magic bytes %PDF', () {
      final bytes = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34];
      expect(ImageValidator.detectMimeType(bytes), isNull);
    });

    test('rejects plain text and arbitrary byte sequences', () {
      final textBytes = 'Hello world this is not an image'.codeUnits;
      expect(ImageValidator.detectMimeType(textBytes), isNull);
    });

    test('rejects sequences with fewer than 3 bytes', () {
      expect(ImageValidator.detectMimeType([]), isNull);
      expect(ImageValidator.detectMimeType([0xFF]), isNull);
      expect(ImageValidator.detectMimeType([0xFF, 0xD8]), isNull);
    });
  });

  group('ImageValidator.validateXFile Tests', () {
    test('accepts valid JPEG within 5 MB limit', () async {
      final jpegBytes = Uint8List.fromList([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46]);
      final file = XFile.fromData(
        jpegBytes,
        name: 'sample.jpg',
        path: '/mock/sample.jpg',
      );

      final result = await ImageValidator.validateXFile(file);
      expect(result.path, equals('/mock/sample.jpg'));
      expect(result.fileName, equals('sample.jpg'));
      expect(result.fileType, equals('image/jpeg'));
      expect(result.sizeBytes, equals(jpegBytes.length));
    });

    test('accepts valid PNG within 5 MB limit', () async {
      final pngBytes = Uint8List.fromList([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00]);
      final file = XFile.fromData(
        pngBytes,
        name: 'sample.png',
        path: '/mock/sample.png',
      );

      final result = await ImageValidator.validateXFile(file);
      expect(result.fileType, equals('image/png'));
    });

    test('accepts valid WebP within 5 MB limit', () async {
      final webpBytes = Uint8List.fromList([
        0x52, 0x49, 0x46, 0x46,
        0x00, 0x00, 0x00, 0x00,
        0x57, 0x45, 0x42, 0x50,
        0x56, 0x50, 0x38, 0x20,
      ]);
      final file = XFile.fromData(
        webpBytes,
        name: 'sample.webp',
        path: '/mock/sample.webp',
      );

      final result = await ImageValidator.validateXFile(file);
      expect(result.fileType, equals('image/webp'));
    });

    test('rejects file larger than 5 MB', () async {
      final file = XFile.fromData(
        Uint8List(0),
        name: 'huge.jpg',
        path: '/mock/huge.jpg',
        length: 5 * 1024 * 1024 + 1, // 5 MB + 1 byte
      );

      expect(
        () => ImageValidator.validateXFile(file),
        throwsA(isA<ImagePickerException>().having(
          (e) => e.message,
          'message',
          'Each photo must be 5 MB or smaller.',
        )),
      );
    });

    test('accepts file exactly 5 MB', () async {
      final prefix = [0xFF, 0xD8, 0xFF, 0xE0];
      final file = XFile.fromData(
        Uint8List.fromList(prefix),
        name: 'exact_5mb.jpg',
        path: '/mock/exact_5mb.jpg',
        length: 5 * 1024 * 1024,
      );

      final result = await ImageValidator.validateXFile(file);
      expect(result.fileType, equals('image/jpeg'));
      expect(result.sizeBytes, equals(5 * 1024 * 1024));
    });

    test('rejects file shorter than 3 bytes', () async {
      final file = XFile.fromData(
        Uint8List.fromList([0xFF, 0xD8]),
        name: 'tiny.jpg',
        path: '/mock/tiny.jpg',
      );

      expect(
        () => ImageValidator.validateXFile(file),
        throwsA(isA<ImagePickerException>().having(
          (e) => e.message,
          'message',
          'Only JPEG, PNG, or WebP photos are supported.',
        )),
      );
    });

    test('rejects disguised text file with .jpg extension', () async {
      final file = XFile.fromData(
        Uint8List.fromList('This is plain text disguised as jpg'.codeUnits),
        name: 'fake.jpg',
        path: '/mock/fake.jpg',
      );

      expect(
        () => ImageValidator.validateXFile(file),
        throwsA(isA<ImagePickerException>().having(
          (e) => e.message,
          'message',
          'Only JPEG, PNG, or WebP photos are supported.',
        )),
      );
    });
  });

  group('ImagePickerException Tests', () {
    test('toString returns message', () {
      const ex = ImagePickerException('Custom error');
      expect(ex.toString(), equals('Custom error'));
    });
  });

  group('DefaultImagePickerService Tests', () {
    late FakeImagePicker fakePicker;
    late DefaultImagePickerService service;

    setUp(() {
      fakePicker = FakeImagePicker();
      service = DefaultImagePickerService(picker: fakePicker);
    });

    test('takePhoto returns validated image on success', () async {
      final jpegBytes = Uint8List.fromList([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]);
      fakePicker.imageToReturn = XFile.fromData(jpegBytes, name: 'camera.jpg', path: '/mock/camera.jpg');

      final result = await service.takePhoto();
      expect(result, isNotNull);
      expect(result!.fileName, equals('camera.jpg'));
      expect(result.fileType, equals('image/jpeg'));
    });

    test('takePhoto returns null on cancellation', () async {
      fakePicker.imageToReturn = null;
      final result = await service.takePhoto();
      expect(result, isNull);
    });

    test('takePhoto throws ImagePickerException on PlatformException', () async {
      fakePicker.throwPlatformException = true;
      expect(
        () => service.takePhoto(),
        throwsA(isA<ImagePickerException>().having(
          (e) => e.message,
          'message',
          'Camera is not available or permission was denied.',
        )),
      );
    });

    test('takePhoto throws ImagePickerException on generic exception', () async {
      fakePicker.throwGenericException = true;
      expect(
        () => service.takePhoto(),
        throwsA(isA<ImagePickerException>().having(
          (e) => e.message,
          'message',
          'Failed to capture photo. Please try again.',
        )),
      );
    });

    test('pickFromGallery returns empty list when maxImages <= 0', () async {
      final result = await service.pickFromGallery(maxImages: 0);
      expect(result, isEmpty);
    });

    test('pickFromGallery returns empty list on cancellation', () async {
      fakePicker.multiImagesToReturn = [];
      final result = await service.pickFromGallery(maxImages: 3);
      expect(result, isEmpty);
    });

    test('pickFromGallery returns validated images respecting maxImages limit', () async {
      final jpegBytes = Uint8List.fromList([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]);
      fakePicker.multiImagesToReturn = [
        XFile.fromData(jpegBytes, name: 'img1.jpg', path: '/mock/img1.jpg'),
        XFile.fromData(jpegBytes, name: 'img2.jpg', path: '/mock/img2.jpg'),
        XFile.fromData(jpegBytes, name: 'img3.jpg', path: '/mock/img3.jpg'),
      ];

      final result = await service.pickFromGallery(maxImages: 2);
      expect(result.length, equals(2));
      expect(result[0].fileName, equals('img1.jpg'));
      expect(result[1].fileName, equals('img2.jpg'));
    });

    test('pickFromGallery throws ImagePickerException on PlatformException', () async {
      fakePicker.throwPlatformException = true;
      expect(
        () => service.pickFromGallery(maxImages: 3),
        throwsA(isA<ImagePickerException>().having(
          (e) => e.message,
          'message',
          'Unable to access photo gallery.',
        )),
      );
    });

    test('pickFromGallery throws ImagePickerException on generic exception', () async {
      fakePicker.throwGenericException = true;
      expect(
        () => service.pickFromGallery(maxImages: 3),
        throwsA(isA<ImagePickerException>().having(
          (e) => e.message,
          'message',
          'Failed to select photos. Please try again.',
        )),
      );
    });

    test('retrieveLostImages returns empty list when no lost data', () async {
      fakePicker.lostDataToReturn = null;
      final result = await service.retrieveLostImages();
      expect(result, isEmpty);
    });

    test('retrieveLostImages recovers and validates lost files', () async {
      final jpegBytes = Uint8List.fromList([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]);
      final file = XFile.fromData(jpegBytes, name: 'lost.jpg', path: '/mock/lost.jpg');
      fakePicker.lostDataToReturn = LostDataResponse(
        file: file,
        files: [file],
        type: RetrieveType.image,
      );

      final result = await service.retrieveLostImages();
      expect(result.length, equals(1));
      expect(result.first.fileName, equals('lost.jpg'));
    });
  });
}

class FakeImagePicker extends ImagePicker {
  XFile? imageToReturn;
  List<XFile> multiImagesToReturn = [];
  LostDataResponse? lostDataToReturn;
  bool throwPlatformException = false;
  bool throwGenericException = false;

  @override
  Future<XFile?> pickImage({
    required ImageSource source,
    double? maxWidth,
    double? maxHeight,
    int? imageQuality,
    CameraDevice preferredCameraDevice = CameraDevice.rear,
    bool requestFullMetadata = true,
  }) async {
    if (throwPlatformException) {
      throw PlatformException(code: 'camera_access_denied', message: 'Permission denied');
    }
    if (throwGenericException) {
      throw Exception('Hardware failure');
    }
    return imageToReturn;
  }

  @override
  Future<List<XFile>> pickMultiImage({
    double? maxWidth,
    double? maxHeight,
    int? imageQuality,
    int? limit,
    bool requestFullMetadata = true,
  }) async {
    if (throwPlatformException) {
      throw PlatformException(code: 'gallery_access_denied', message: 'Permission denied');
    }
    if (throwGenericException) {
      throw Exception('Storage error');
    }
    if (limit != null && limit > 0) {
      return multiImagesToReturn.take(limit).toList();
    }
    return multiImagesToReturn;
  }

  @override
  Future<LostDataResponse> retrieveLostData() async {
    return lostDataToReturn ?? LostDataResponse.empty();
  }
}
