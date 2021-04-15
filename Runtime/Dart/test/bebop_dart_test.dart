import 'package:bebop_dart/bebop_dart.dart';
import 'package:bebop_dart/stacked_schemes.g.dart';
import 'package:test/test.dart';

void main() {
  group('Bebop', () {
    BebopWriter writer;
    BebopReader reader;

    setUp(() {});

    test('Basic roundtrip', () {
      writer = BebopWriter();
      writer.writeByte(5);
      writer.writeUint32(123);
      writer.writeBool(true);
      final list = writer.toList();
      expect(list, equals([5, 123, 0, 0, 0, 1]));
      reader = BebopReader(list);
      expect(reader.readByte(), equals(5));
      expect(reader.readUint32(), equals(123));
      expect(reader.readBool(), equals(true));
    });

    test('String', () {
      writer = BebopWriter();
      writer.writeString('aあ🎯');
      final list = writer.toList();
      expect(list, equals([8, 0, 0, 0, 97, 227, 129, 130, 240, 159, 142, 175]));
      reader = BebopReader(list);
      expect(reader.readString(), equals('aあ🎯'));
    });

    test('Date', () {
      writer = BebopWriter();
      final date = DateTime(2020, 1, 2, 3, 45, 678);
      writer.writeDate(date);
      final list = writer.toList();
      expect(list, hasLength(8));
      reader = BebopReader(list);
      expect(reader.readDate().difference(date).inMilliseconds, equals(0));
    });

    test('GUID', () {
      writer = BebopWriter();
      final guid = '00112233-aabb-ccdd-3344-5566778899aa';
      writer.writeGuid(guid);
      final list = writer.toList();
      expect(list, hasLength(16));
      reader = BebopReader(list);
      expect(reader.readGuid(), equals(guid));
    });

    test('Stacked schemes', () {
      var scheme1 = Schema1(prop1: "Hi I'm stacked");
      var scheme2 = Schema2(schemaBytes: Schema1.encode(scheme1));

      var bytes = Schema2.encode(scheme2);
      var restored2 = Schema2.decode(bytes);
      expect(restored2.schemaBytes, isNotNull);

      var restored1 = Schema1.decode(restored2.schemaBytes);
      expect(restored1.prop1, equals("Hi I'm stacked"));
    });
  });
}
