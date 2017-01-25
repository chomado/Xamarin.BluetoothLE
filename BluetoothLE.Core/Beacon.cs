using System;
namespace BluetoothLE.Core
{
    public class Beacon
    {
        private const int MinimumLengthInBytes = 25;//最小の長さ
        private const int AdjustedLengthInBytes = -2;//CompanyID分の2桁ずれている為読み取り位置補正

        //プロパティ
        public string Name { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        //public BluetoothLEAdvertisementType AdvertisementType { get; set; }

        public int ManufacturerId { get; set; }
        public int Major { get; set; }
        public int Minor { get; set; }
        public string UUID { get; set; }
        public short Rssi { get; set; }
        public short MeasuredPower { get; set; }
        public double ManufacturerReserved { get; set; }

        //精度（accuracy）
        public double Accuracy
        {
            get { return CalcAccuracy(MeasuredPower, Rssi); }
        }

        //近接度（Proximity）：近接（immidiate）、1m以内（near）、1m以遠（far）、不明（Unknown）
        public string Proximity
        {
            get
            {
                string _Proximity = "Unknown";

                //Rssi未取得ならUnknown
                if (Rssi == 0) { return _Proximity; }

                //rssi値からProximityを判別
                if (Rssi > -40)
                {
                    _Proximity = "immidiate";//近接
                }
                else if (Rssi > -59)
                {
                    _Proximity = "near";//1m以内
                }
                else
                {
                    _Proximity = "far";//1m以遠
                }
                return _Proximity;
            }
        }

        //コンストラクタ
        public Beacon(byte[] scanRecord)
        {
            var ids = ConvertBltToBeacon(scanRecord);

            ManufacturerId = -1;
            Major = ids?.Major ??  -1;
            Minor = ids?.Minor ?? -1;
            Rssi = 0;
            UUID = ids?.Uuid ?? ""; 
            MeasuredPower = -1;
            ManufacturerReserved = -1.0;
        }

        /*
        //コンストラクタ２
        public Beacon(BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {

            //出力されているbyteデータから各値を抽出する
            var manufacturerSections = eventArgs.Advertisement.ManufacturerData;
            Timestamp = eventArgs.Timestamp;
            AdvertisementType = eventArgs.AdvertisementType;

            if (manufacturerSections.Count > 0)
            {
                var manufacturerData = manufacturerSections[0];
                var data = new byte[manufacturerData.Data.Length];

                iBeacon bcon = new iBeacon();

                using (var reader = DataReader.FromBuffer(manufacturerData.Data))
                {
                    reader.ReadBytes(data);
                }

                //長さをチェック
                if (data == null || data.Length < MinimumLengthInBytes + AdjustedLengthInBytes)
                {
                    return;
                }

                //イベントから取得
                Rssi = eventArgs.RawSignalStrengthInDBm;
                Name = eventArgs.Advertisement.LocalName;
                ManufacturerId = manufacturerData.CompanyId;

                //バイトデータから抽出
                //公式での出力値（Windowsでは2byteずれているので補正が必要）
                // Byte(s)  WinByte(s) Name
                // --------------------------
                // 0-1      none       Manufacturer ID (16-bit unsigned integer, big endian)
                // 2-3      0-1        Beacon code (two 8-bit unsigned integers, but can be considered as one 16-bit unsigned integer in little endian)
                // 4-19     2-17       ID1 (UUID)
                // 20-21    18-19      ID2 (16-bit unsigned integer, big endian)
                // 22-23    20-21      ID3 (16-bit unsigned integer, big endian)
                // 24       22         Measured Power (signed 8-bit integer)
                // 25       23         Reserved for use by the manufacturer to implement special features (optional)

                //BigEndianの値を取得
                UUID = BitConverter.ToString(data, 4 + AdjustedLengthInBytes, 16); // Bytes 2-17
                MeasuredPower = Convert.ToSByte(BitConverter.ToString(data, 24 + AdjustedLengthInBytes, 1), 16); // Byte 22

                //もし追加データがあればここで取得
                if (data.Length >= MinimumLengthInBytes + AdjustedLengthInBytes + 1)
                {
                    ManufacturerReserved = data[25 + AdjustedLengthInBytes]; // Byte 23
                }

                //.NET FramewarkのEndianはCPUに依存するらしい
                if (BitConverter.IsLittleEndian)
                {
                    //LittleEndianの値を取得
                    byte[] revData;

                    revData = new byte[] { data[20 + AdjustedLengthInBytes], data[21 + AdjustedLengthInBytes] };// Bytes 18-19
                    Array.Reverse(revData);
                    Major = BitConverter.ToUInt16(revData, 0);

                    revData = new byte[] { data[22 + AdjustedLengthInBytes], data[23 + AdjustedLengthInBytes] };// Bytes 20-21
                    Array.Reverse(revData);
                    Minor = BitConverter.ToUInt16(revData, 0);
                }
                else
                {
                    //BigEndianの値を取得
                    Major = BitConverter.ToUInt16(data, 20 + AdjustedLengthInBytes); // Bytes 18-19
                    Minor = BitConverter.ToUInt16(data, 22 + AdjustedLengthInBytes); // Bytes 20-21
                }
            }
            else
            {
                new Beacon();
            }
        }
        */

        //精度を計算する
        protected static double CalcAccuracy(short measuredPower, short rssi)
        {
            if (rssi == 0)
            {
                return -1.0; //nodata return -1.
            }

            double ratio = rssi * 1.0 / measuredPower;
            if (ratio < 1.0)
            {
                return Math.Pow(ratio, 10);
            }
            else
            {
                double accuracy = (0.89976) * Math.Pow(ratio, 7.7095) + 0.111;
                return accuracy;
            }
        }

        public BeaconIds ConvertBltToBeacon(byte[] scanRecord)
        { 
            if (scanRecord.Length > 30)
            {
                //iBeacon の場合 6 byte 目から、 9 byte 目はこの値に固定されている。
                if ((scanRecord[5] == (byte)0x4c) && (scanRecord[6] == (byte)0x00) &&
                (scanRecord[7] == (byte)0x02) && (scanRecord[8] == (byte)0x15))
                {
                    String uuid = IntToHex2(scanRecord[9] & 0xff)
                                + IntToHex2(scanRecord[10] & 0xff)
                                + IntToHex2(scanRecord[11] & 0xff)
                                + IntToHex2(scanRecord[12] & 0xff)
                                + "-"
                                + IntToHex2(scanRecord[13] & 0xff)
                                + IntToHex2(scanRecord[14] & 0xff)
                                + "-"
                                + IntToHex2(scanRecord[15] & 0xff)
                                + IntToHex2(scanRecord[16] & 0xff)
                                + "-"
                                + IntToHex2(scanRecord[17] & 0xff)
                                + IntToHex2(scanRecord[18] & 0xff)
                                + "-"
                                + IntToHex2(scanRecord[19] & 0xff)
                                + IntToHex2(scanRecord[20] & 0xff)
                                + IntToHex2(scanRecord[21] & 0xff)
                                + IntToHex2(scanRecord[22] & 0xff)
                                + IntToHex2(scanRecord[23] & 0xff)
                                + IntToHex2(scanRecord[24] & 0xff);

                    String major = IntToHex2(scanRecord[25] & 0xff) + IntToHex2(scanRecord[26] & 0xff);
                    String minor = IntToHex2(scanRecord[27] & 0xff) + IntToHex2(scanRecord[28] & 0xff);

                    return new BeaconIds()
                    {
                        Uuid = uuid,
                        Major = int.Parse(major),
                        Minor = int.Parse(minor)
                    };
                }
            }
            return null;
        }
        //intデータを 2桁16進数に変換するメソッド
        String IntToHex2(int i)
        {
            char[] hex_2 = { Character.forDigit((i >> 4) & 0x0f, 16), Character.forDigit(i & 0x0f, 16) };
            String hex_2_str = new String(hex_2);
            return hex_2_str.ToUpper();
        }
    }

    // Java の　java.lang.Character.forDigit(int digit, int radix)の自前実装
    class Character
    {
        public const int MIN_RADIX = 2;
        public const int MAX_RADIX = 36;

        public static char forDigit(int digit, int radix)
        {
            if (radix < MIN_RADIX || radix > MAX_RADIX)
                throw new ArgumentOutOfRangeException(nameof(radix));

            if (digit < 0 || digit >= radix)
                throw new ArgumentOutOfRangeException(nameof(digit));

            if (digit < 10)
                return (char)(digit + (int)'0');

            return (char)(digit - 10 + (int)'a');
        }
    }

    public class BeaconIds
    {
        public string Uuid;
        public int Major;
        public int Minor;
    }
}
