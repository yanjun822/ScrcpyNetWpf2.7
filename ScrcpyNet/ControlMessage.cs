using ScrcpyNet;
using System;
using System.Buffers.Binary;

namespace ScrcpyNet
{

    public enum ControlMessageType : byte
    {
        InjectKeycode,
        InjectText,
        InjectTouchEvent,
        InjectScrollEvent,
        BackOrScreenOn,
        ExpandNotificationPanel,
        ExpandSettingsPanel,
        CollapsePanels,
        GetClipboard,
        SetClipboard,
        SetScreenPowerMode,
        RotateDevice,
    }

    public struct ScreenSize
    {
        public ushort Width;
        public ushort Height;
    }

    public struct Point
    {
        public int X;
        public int Y;
    }

    // Not sure whether to use struct, record, or class for this.
    public class Position
    {
        public ScreenSize ScreenSize = new ScreenSize();
        public Point Point = new Point();

        public Span<byte> ToBytes()
        {
            Span<byte> b = new byte[12];
            BinaryPrimitives.WriteInt32BigEndian(b.Slice(0), Point.X);
            BinaryPrimitives.WriteInt32BigEndian(b.Slice(4), Point.Y);
            BinaryPrimitives.WriteUInt16BigEndian(b.Slice(8), ScreenSize.Width);
            BinaryPrimitives.WriteUInt16BigEndian(b.Slice(10), ScreenSize.Height);
            return b;
        }
    }

    public interface IControlMessage
    {
        ControlMessageType Type { get; }

        Span<byte> ToBytes();
    }

    public class KeycodeControlMessage : IControlMessage
    {
        public ControlMessageType Type => ControlMessageType.InjectKeycode;
        public AndroidKeyEventAction Action { get; set; }
        public AndroidKeycode KeyCode { get; set; }
        public uint Repeat { get; set; }
        public AndroidMetastate Metastate { get; set; }

        public Span<byte> ToBytes()
        {
            Span<byte> b = new byte[14];
            b[0] = (byte)Type;
            b[1] = (byte)Action;
            BinaryPrimitives.WriteInt32BigEndian(b.Slice(2), (int)KeyCode);
            BinaryPrimitives.WriteInt32BigEndian(b.Slice(6), (int)Repeat);
            BinaryPrimitives.WriteInt32BigEndian(b.Slice(10), (int)Metastate);
            return b;
        }
    }

    public class BackOrScreenOnControlMessage : IControlMessage
    {
        public ControlMessageType Type => ControlMessageType.BackOrScreenOn;
        public AndroidKeyEventAction Action { get; set; }

        public Span<byte> ToBytes()
        {
            Span<byte> b = new byte[2];
            b[0] = (byte)Type;
            b[1] = (byte)Action;
            return b;
        }
    }

    public class TouchEventControlMessage : IControlMessage
    {
        public ControlMessageType Type => ControlMessageType.InjectTouchEvent;
        public AndroidMotionEventAction Action { get; set; }
        public AndroidMotionEventButtons Buttons { get; set; } = AndroidMotionEventButtons.AMOTION_EVENT_BUTTON_PRIMARY;
        public ulong PointerId { get; set; } = 0xFFFFFFFFFFFFFFFF;
        public Position Position { get; set; } = new Position();
        //public float Pressure { get; set; }

        public Span<byte> ToBytes()
        {
            Span<byte> b = new byte[32];
            b[0] = (byte)Type;
            b[1] = (byte)Action;
            BinaryPrimitives.WriteUInt64BigEndian(b.Slice(2), PointerId);

            // Position
            BinaryPrimitives.WriteInt32BigEndian(b.Slice(10), Position.Point.X);
            BinaryPrimitives.WriteInt32BigEndian(b.Slice(14), Position.Point.Y);
            BinaryPrimitives.WriteUInt16BigEndian(b.Slice(18), Position.ScreenSize.Width);
            BinaryPrimitives.WriteUInt16BigEndian(b.Slice(20), Position.ScreenSize.Height);

            // TODO: Pressure 压力
            b[22] = 0xFF;
            b[23] = 0xFF;
            //action button  
            BinaryPrimitives.WriteInt32BigEndian(b.Slice(24), (int)(0));
            //Buttons
            BinaryPrimitives.WriteInt32BigEndian(b.Slice(28), (int)Buttons);

            return b;
        }
    }

    public class ScrollEventControlMessage : IControlMessage
    {
        public ControlMessageType Type => ControlMessageType.InjectScrollEvent;
        public Position Position { get; set; } = new Position();
        public float HorizontalScroll { get; set; }
        public float VerticalScroll { get; set; }
        public AndroidMotionEventButtons Buttons { get; set; } = AndroidMotionEventButtons.AMOTION_EVENT_BUTTON_PRIMARY;

        public Span<byte> ToBytes()
        {
            Span<byte> b = new byte[21];
            b[0] = (byte)Type;
            Position.ToBytes().CopyTo(b.Slice(1));

            short h = (short)Math.Round(HorizontalScroll * 32767);
            short v = (short)Math.Round(VerticalScroll * 32767);
            BinaryPrimitives.WriteInt16BigEndian(b.Slice(13),h);
            BinaryPrimitives.WriteInt16BigEndian(b.Slice(15), v);
               //Buttons
            BinaryPrimitives.WriteInt32BigEndian(b.Slice(17), (int)Buttons);

            return b;
        }
    }
}
