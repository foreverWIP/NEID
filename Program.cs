using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NEID
{
    class Program
    {
        public static IntPtr VRAM { get; private set; }
        public static IntPtr FileBuffer { get; private set; }
        public static IntPtr TempBuffer { get; private set; }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: NEID <input file path> <offset to read data from, in hex or decimal>");
        }

        static void Main(string[] args)
        {
            if (args[0] == "-h" || args[0] == "--help")
            {
                Console.WriteLine("NEID (Nights Ending Image Decompressor)");
                Console.WriteLine("By Amy Wright and contributors");
                PrintUsage();
                return;
            }

            if (!File.Exists(args[0]))
            {
                Console.WriteLine("Unable to find input file.");
                Console.WriteLine("Make sure it exists and that the path was typed correctly.");
                PrintUsage();
                return;
            }

            var compressed = File.ReadAllBytes(args[0]);

            uint fileOffset = 0;
            try
            {
                if (args[1].StartsWith("0x"))
                {
                    fileOffset = Convert.ToUInt32(args[1], 16);
                }
                else
                {
                    fileOffset = Convert.ToUInt32(args[1], 10);
                }
                if (fileOffset >= compressed.Length)
                {
                    Console.WriteLine("Offset specified is at or larger than the size of the file holding the data.");
                    Console.WriteLine("Make sure you're using the right file with the offset.");
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Unable to parse offset. Make sure the value is in the right format.");
                Console.WriteLine("Offset can be decimal (digits are 0-9) or hex (0-9 or a-f).");
                Console.WriteLine("For hex offsets, include the leading '0x'.");
            }

            VRAM = Marshal.AllocHGlobal(0x10_0000);
            TempBuffer = Marshal.AllocHGlobal(0x400);

            FileBuffer = Marshal.AllocHGlobal(compressed.Length);
            Marshal.Copy(compressed, 0, FileBuffer, compressed.Length);

            var (dataWidth, dataHeight) = DecompressEndingImage(0, 0, fileOffset, 1);

            var fileBuffer = new byte[0x10_0000];
            Marshal.Copy(VRAM, fileBuffer, 0, fileBuffer.Length);
            File.WriteAllBytes($"{Path.GetFileNameWithoutExtension(args[0])}_{fileOffset:X8}_raw.bin", fileBuffer);

            var vramManaged = new short[0x10_0000 / 4];
            Marshal.Copy(VRAM, vramManaged, 0, vramManaged.Length);

            var outputImage = new Image<Rgba32>((int)dataWidth, (int)dataHeight);

            for (var y = 0; y < dataHeight; y++)
            {
                for (var x = 0; x < dataWidth; x++)
                {
                    var rawPixel = vramManaged[(y * VRAM_WIDTH_BYTES / 2) + x];
                    var colorIndex = (byte)((rawPixel & 0xff00) >> 8);
                    var luminosity = (byte)((rawPixel & 0x00ff) >> 0);
                    outputImage[x, y] = new Rgba32(luminosity, luminosity, luminosity, 255);
                }
            }

            outputImage.SaveAsPng($"{Path.GetFileNameWithoutExtension(args[0])}_{fileOffset:X8}.png");
            Cleanup();
        }

        public static void Cleanup()
        {
            Marshal.FreeHGlobal(FileBuffer);
            Marshal.FreeHGlobal(TempBuffer);
            Marshal.FreeHGlobal(VRAM);
        }

        public const uint TMP_BUFFER_PTR = 0x60cb100;
        public const uint VRAM_START_PTR = 0x5e00000;
        const uint VRAM_HALFWAY_PTR = 0x05e40000;
        const ushort DATA_MAX_WIDTH = 0x01ff;
        const ushort DATA_MAX_HEIGHT = 0x00ff;
        public const ushort VRAM_WIDTH_BYTES = 0x0400;

        // FUN_06067324
        static (uint, uint) DecompressEndingImage(
            uint vramXOffset,
            uint vramYOffset,
            uint compressedDataPtr,
            uint options
        )
        {
            // the only out-of-the-box decompiler for sh2 i could find (ghidra's decompiler)
            // has a lot of trouble figuring this routine out, so for now, just to have something
            // working, we're doing this the hard way: hand-translated assembly
            // consider this "pre-emulated" sh2 code; every instruction in the listing is
            // provided with its corresponding logic in c# syntax
            // the class below this one holds extension methods for calculating reads and writes
            uint r0 = 0xdeadbeef;
            uint r1 = 0xdeadbeef;
            uint r2 = 0xdeadbeef;
            uint r3 = 0xdeadbeef;
            uint r4 = vramXOffset;
            uint r5 = vramYOffset;
            uint r6 = compressedDataPtr;
            uint r7 = options;
            uint r8 = 0xdeadbeef;
            uint r9 = 0xdeadbeef;
            uint r10 = 0xdeadbeef;
            uint r11 = 0xdeadbeef;
            uint r12 = 0xdeadbeef;
            uint r13 = 0xdeadbeef;
            uint r14 = 0xdeadbeef;
            uint macl = 0xdeadbeef;
            bool t = false;
            ushort stack0 = 0xdead;
            ushort stack2 = 0xdead;
            uint stack4 = 0xdeadbeef;
            uint stack8 = 0xdeadbeef;
            uint stackc = 0xdeadbeef;
            uint stack10 = 0xdeadbeef;
            uint stack14 = 0xdeadbeef;
            uint stack18 = 0xdeadbeef;

            // 06067344 db 22           mov.l      @(TMP_BUFFER_PTR,pc),r11=>DAT_060cb100           = 060cb100
            r11 = TMP_BUFFER_PTR;
            // 06067346 6e 63           mov        r6,r14
            r14 = r6;
            // 06067348 1f 65           mov.l      r6,@(0x14,r15)
            stack14 = r6;
            // 0606734a 60 73           mov        r7,r0
            r0 = r7;
            // 0606734c 81 f2           mov.w      r0,@(0x4,r15)
            stack4 = (ushort)r0;
            // 0606734e e7 20           mov        #32,r7
            r7 = 32;
            // 06067350 66 e5           mov.w      @r14+,r6
            r6 = (uint)(short)r14.read16(); r14 += 2;
            // 06067352 60 63           mov        r6,r0
            r0 = r6;
            // 06067354 66 e5           mov.w      @r14+,r6
            r6 = (uint)(short)r14.read16(); r14 += 2;
            // 06067356 60 63           mov        r6,r0
            r0 = r6;
            // 06067358 66 e5           mov.w      @r14+,r6
            r6 = (uint)(short)r14.read16(); r14 += 2;
            // 0606735a 88 0f           cmp/eq     0xf,r0
            t = r0 == 0xf;
            // 0606735c 8b d0           bf         GetStackValues
            if (!t) return (0, 0);
            // 0606735e 60 63           mov        r6,r0
            r0 = r6;
            // 06067360 66 e5           mov.w      @r14+,r6
            r6 = (uint)(short)r14.read16(); r14 += 2;
            // 06067362 1f 02           mov.l      r0,@(0x8,r15)
            stack8 = r0;
            // 06067364 70 ff           add        -0x1,r0
            r0--;
            // 06067366 61 03           mov        r0,r1
            r1 = r0;
            // 06067368 60 63           mov        r6,r0
            r0 = r6;
            // 0606736a 66 e6           mov.l      @r14+,r6
            r6 = r14.read32(); r14 += 4;
            // 0606736c 1f 03           mov.l      r0,@(0xc,r15)
            stackc = r0;
            // 0606736e 70 ff           add        -0x1,r0
            r0--;
            // 06067370 62 03           mov        r0,r2
            r2 = r0;
            // 06067372 31 4c           add        r4,r1
            r1 += r4;
            // 06067374 90 28           mov.w      @(DATA_MAX_WIDTH,pc),r0                          = 1FFh
            r0 = (uint)(short)DATA_MAX_WIDTH;
            // 06067376 31 06           cmp/hi     r0,r1
            t = r1 > r0;
            // 06067378 89 c2           bt         GetStackValues
            if (t) return (0, 0);

            var dataWidth = r1 + 1;

            // 0606737a 32 5c           add        r5,r2
            r2 += r5;
            // 0606737c 90 25           mov.w      @(DATA_MAX_HEIGHT,pc),r0                         = FFh
            r0 = (uint)(short)DATA_MAX_HEIGHT;
            // 0606737e 32 06           cmp/hi     r0,r2
            t = r2 > r0;
            // 06067380 89 be           bt         GetStackValues
            if (t) return (0, 0);

            var dataHeight = r2 + 1;

            // 06067382 90 23           mov.w      @(VRAM_WIDTH_BYTES,pc),r0                        = 400h
            r0 = (uint)(short)VRAM_WIDTH_BYTES;
            // 06067384 25 0e           mulu.w     r0,r5
            macl = r5 * r0;
            // 06067386 44 00           shll       r4
            t = (r4 & 0x8000_0000) != 0; r4 <<= 1;
            // 06067388 85 f2           mov.w      @(0x4,r15),r0
            r0 = (uint)(short)stack4;
            // 0606738a c8 08           tst        0x8,r0
            t = (r0 & 0x8) == 0;
            // 0606738c d0 11           mov.l      @(VRAM_START_PTR,pc),r0                          = 05e00000
            r0 = VRAM_START_PTR;
            // 0606738e 89 00           bt         LAB_06067392
            if (t) goto LAB_06067392;
            // 06067390 d0 11           mov.l      @(VRAM_HALFWAY_PTR,pc),r0                        = 05e40000
            r0 = VRAM_HALFWAY_PTR;
        LAB_06067392:
            // 06067392 05 1a           sts        macl,r5
            r5 = macl;
            // 06067394 30 4c           add        r4,r0
            r0 += r4;
            // 06067396 30 5c           add        r5,r0
            r0 += r5;
            // 06067398 1f 04           mov.l      r0,@(0x10,r15)
            stack10 = r0;
            // 0606739a 85 f2           mov.w      @(0x4,r15),r0
            r0 = (uint)(short)stack4;
            // 0606739c c8 01           tst        0x1,r0
            t = (r0 & 0x1) == 0;
            // 0606739e 89 01           bt         LAB_060673a4
            if (t) goto LAB_060673a4;
            // 060673a0 b1 10           bsr        InitializeVRAM
            // 060673a2 00 09           _nop
            void InitializeVRAM()
            {
                // 060675c4 51 f4           mov.l      @(0x10,r15),r1
                r1 = stack10;
                // 060675c6 53 f2           mov.l      @(0x8,r15),r3
                r3 = stack8;
                // 060675c8 73 ff           add        -0x1,r3
                r3--;
                // 060675ca 33 3c           add        r3,r3
                r3 += r3;
                // 060675cc 54 f3           mov.l      @(0xc,r15),r4
                r4 = stackc;
                // 060675ce 98 0a           mov.w      @(DAT_060675e6,pc),r8                            = 0400h
                r8 = (uint)(short)VRAM_WIDTH_BYTES;
                // 060675d0 e5 00           mov        #0x0,r5
                r5 = 0;
            LAB_060675d2:
                // 060675d2 60 33           mov        r3,r0
                r0 = r3;
            LAB_060675d4:
                // 060675d4 01 55           mov.w      r5,@(r0,r1)
                (r0 + r1).write16(r5);
                // 060675d6 70 fe           add        -0x2,r0
                r0 -= 2;
                // 060675d8 40 11           cmp/pz     r0
                t = (int)r0 >= 0;
                // 060675da 89 fb           bt         LAB_060675d4
                if (t) goto LAB_060675d4;
                // 060675dc 31 8c           add        r8,r1
                r1 += r8;
                // 060675de 44 10           dt         r4
                r4--; t = r4 == 0;
                // 060675e0 8b f7           bf         LAB_060675d2
                if (!t) goto LAB_060675d2;
                // 060675e2 00 0b           rts
                // 060675e4 00 09           _nop
            }
            InitializeVRAM();
        LAB_060673a4:
            // 060673a4 b0 f4           bsr        InitializeTempBuffer
            // 060673a6 00 09           _nop
            void InitializeTempBuffer()
            {
                // 06067590 e1 00           mov        #0x0,r1
                r1 = 0;
                // 06067592 92 13           mov.w      @(DAT_060675bc,pc),r2                            = 0100h
                r2 = (uint)(short)0x100;
                // 06067594 93 13           mov.w      @(DAT_060675be,pc),r3                            = 00FCh
                r3 = (uint)(short)0xfc;
                // 06067596 3b 2c           add        r2,r11
                r11 += r2;
                // 06067598 6c 23           mov        r2,r12
                r12 = r2;
                // 0606759a 3c bc           add        r11,r12
                r12 += r11;
                // 0606759c 6d 23           mov        r2,r13
                r13 = r2;
                // 0606759e 3d cc           add        r12,r13
                r13 += r12;
                // 060675a0 90 0e           mov.w      @(DAT_060675c0,pc),r0                            = 0080h
                r0 = (uint)(short)0x80;
            LAB_060675a2:
                // 060675a2 2b 15           mov.w      r1,@-r11
                r11 -= 2; r11.write16(r1);
                // 060675a4 2c 25           mov.w      r2,@-r12
                r12 -= 2; r12.write16(r2);
                // 060675a6 2d 35           mov.w      r3,@-r13
                r13 -= 2; r13.write16(r3);
                // 060675a8 72 fe           add        -0x2,r2
                r2 -= 2;
                // 060675aa 40 10           dt         r0
                r0--; t = r0 == 0;
                // 060675ac 8f f9           bf/s       LAB_060675a2
                // 060675ae 73 fe           _add       -0x2,r3
                if (!t) { r3 -= 2; goto LAB_060675a2; } else { r3 -= 2; }
                // 060675b0 81 f0           mov.w      r0,@(0x0,r15)
                stack0 = (ushort)r0;
                // 060675b2 2d 05           mov.w      r0,@-r13
                r13 -= 2; r13.write16(r0);
                // 060675b4 90 05           mov.w      @(USHORT_060675c2,pc),r0                         = FEh
                r0 = (uint)(short)0xfe;
                // 060675b6 7d 02           add        0x2,r13
                r13 += 2;
                // 060675b8 00 0b           rts
                // 060675ba 2d 01           _mov.w     r0,@r13
                r13.write16(r0);
                return;
            }
            InitializeTempBuffer();
            // 060673a8 98 10           mov.w      @(VRAM_WIDTH_BYTES,pc),r8                        = 400h
            r8 = (uint)(short)VRAM_WIDTH_BYTES;
            // 060673aa 50 f4           mov.l      @(0x10,r15),r0
            r0 = stack10;
            // 060673ac b0 da           bsr        GetBitRun
            // 060673ae 1f 06           _mov.l     r0,@(0x18,r15)
            stack18 = r0;
            void GetBitRun()
            {
                // 06067564 e0 00           mov        #0x0,r0
                r0 = 0;
                // 06067566 e3 01           mov        #0x1,r3
                r3 = 1;
            LAB_06067568:
                // 06067568 47 10           dt         r7
                r7--; t = r7 == 0;
                // 0606756a 8f 02           bf/s       LAB_06067572
                // 0606756c 46 00           _shll      r6
                if (!t) { t = (r6 & 0x8000_0000) != 0; r6 <<= 1; goto LAB_06067572; } else { t = (r6 & 0x8000_0000) != 0; r6 <<= 1; }
                // 0606756e 66 e6           mov.l      @r14+,r6
                r6 = r14.read32(); r14 += 4;
                // 06067570 e7 20           mov        #32,r7
                r7 = 32;
            LAB_06067572:
                // 06067572 70 01           add        0x1,r0
                r0++;
                // 06067574 8d f8           bt/s       LAB_06067568
                // 06067576 43 00           _shll      r3
                if (t) { t = (r3 & 0x8000_0000) != 0; r3 <<= 1; goto LAB_06067568; } else { t = (r3 & 0x8000_0000) != 0; r3 <<= 1; }
                // 06067578 73 ff           add        -0x1,r3
                r3--;
            LAB_0606757a:
                // 0606757a 47 10           dt         r7
                r7--; t = r7 == 0;
                // 0606757c 8f 02           bf/s       LAB_06067584
                // 0606757e 46 00           _shll      r6
                if (!t) { t = (r6 & 0x8000_0000) != 0; r6 <<= 1; goto LAB_06067584; } else { t = (r6 & 0x8000_0000) != 0; r6 <<= 1; }
                // 06067580 66 e6           mov.l      @r14+,r6
                r6 = r14.read32(); r14 += 4;
                // 06067582 e7 20           mov        #32,r7
                r7 = 32;
            LAB_06067584:
                // 06067584 41 24           rotcl      r1
                { var tmp = (r1 & 0x8000_0000) != 0; r1 <<= 1; if (t) { r1 |= 1; } else { r1 &= 0xfffffffe; } t = tmp; }
                // 06067586 40 10           dt         r0
                r0--; t = r0 == 0;
                // 06067588 8b f7           bf         LAB_0606757a
                if (!t) goto LAB_0606757a;
                // 0606758a 21 39           and        r3,r1
                r1 &= r3;
                // 0606758c 00 0b           rts
                // 0606758e 33 1c           _add       r1,r3
                r3 += r1;
                return;
            }
            GetBitRun();
            // 060673b0 50 f3           mov.l      @(0xc,r15),r0
            r0 = stackc;
        LAB_060673b2:
            // 060673b2 81 f1           mov.w      r0,@(0x2,r15)
            stack2 = (ushort)r0;
            // 060673b4 50 f6           mov.l      @(0x18,r15),r0
            r0 = stack18;
            // 060673b6 69 03           mov        r0,r9
            r9 = r0;
            // 060673b8 30 8c           add        r8,r0
            r0 += r8;
            // 060673ba 1f 06           mov.l      r0,@(0x18,r15)
            stack18 = r0;
            // 060673bc 50 f2           mov.l      @(0x8,r15),r0
            r0 = stack8;
            // 060673be 64 03           mov        r0,r4
            r4 = r0;
        LAB_060673c0:
            // 060673c0 33 46           cmp/hi     r4,r3
            t = r3 > r4;
            // 060673c2 8b 0b           bf         LAB_060673dc
            if (!t) goto LAB_060673dc;
            // 060673c4 a0 9b           bra        LAB_060674fe
            // 060673c6 00 09           _nop
            goto LAB_060674fe;
        LAB_060673dc:
            // 060673dc 34 38           sub        r3,r4
            r4 -= r3;
            // 060673de 73 ff           add        -0x1,r3
            r3--;
            // 060673e0 23 38           tst        r3,r3
            t = (r3 & r3) == 0;
            // 060673e2 89 01           bt         LAB_060673e8
            if (t) goto LAB_060673e8;
            // 060673e4 b0 b0           bsr        FlipWords
            // 060673e6 65 33           _mov       r3,r5
            r5 = r3;
            void FlipWords()
            {
                // 06067548 e0 fe           mov        #-0x2,r0
                r0 = unchecked((uint)-2);
            LAB_0606754a:
                // 0606754a 61 95           mov.w      @r9+,r1
                r1 = (uint)(short)r9.read16(); r9 += 2;
                // 0606754c 41 11           cmp/pz     r1
                t = (int)r1 >= 0;
                // 0606754e 8f 04           bf/s       LAB_0606755a
                // 06067550 45 10           _dt        r5
                if (!t) { r5--; t = r5 == 0; goto LAB_0606755a; } else { r5--; t = r5 == 0; }
                // 06067552 8f fa           bf/s       LAB_0606754a
                // 06067554 09 25           _mov.w     r2,@(r0,r9)
                if (!t) { (r0 + r9).write16(r2); goto LAB_0606754a; } else { (r0 + r9).write16(r2); }
                // 06067556 00 0b           rts
                // 06067558 00 09           _nop
                return;
            LAB_0606755a:
                // 0606755a 62 17           not        r1,r2
                r2 = ~r1;
                // 0606755c 8f f5           bf/s       LAB_0606754a
                // 0606755e 09 25           _mov.w     r2,@(r0,r9)
                if (!t) { (r0 + r9).write16(r2, true); goto LAB_0606754a; } else { (r0 + r9).write16(r2, true); }
                // 06067560 00 0b           rts
                // 06067562 00 09           _nop
                return;
            }
            FlipWords();
        LAB_060673e8:
            // 060673e8 46 11           cmp/pz     r6
            t = (int)r6 >= 0;
            // 060673ea 8b 1c           bf         LAB_06067426
            if (!t) goto LAB_06067426;
            // 060673ec e0 10           mov        #0x10,r0
            r0 = 0x10;
            // 060673ee 37 02           cmp/hs     r0,r7
            t = r7 >= r0;
            // 060673f0 8b 08           bf         LAB_06067404
            if (!t) goto LAB_06067404;
            // 060673f2 62 63           mov        r6,r2
            r2 = r6;
            // 060673f4 42 29           shlr16     r2
            r2 >>= 16;
            // 060673f6 77 f0           add        -0x10,r7
            r7 -= 0x10;
            // 060673f8 27 78           tst        r7,r7
            t = (r7 & r7) == 0;
            // 060673fa 8f 0f           bf/s       LAB_0606741c
            // 060673fc 46 28           _shll16    r6
            if (!t) { r6 <<= 16; goto LAB_0606741c; } else { r6 <<= 16; }
            // 060673fe e7 20           mov        #32,r7
            r7 = 32;
            // 06067400 a0 0c           bra        LAB_0606741c
            // 06067402 66 e6           _mov.l     @r14+,r6
            r6 = r14.read32(); r14 += 4;
            goto LAB_0606741c;
        LAB_06067404:
            // 06067404 46 29           shlr16     r6
            r6 >>= 16;
            // 06067406 60 63           mov        r6,r0
            r0 = r6;
            // 06067408 66 e6           mov.l      @r14+,r6
            r6 = r14.read32(); r14 += 4;
            // 0606740a 6a 7b           neg        r7,r10
            r10 = 0 - r7;
            // 0606740c 7a 10           add        0x10,r10
            r10 += 0x10;
            // 0606740e 77 10           add        0x10,r7
            r7 += 0x10;
            // 06067410 e2 00           mov        #0x0,r2
            r2 = 0;
        LAB_06067412:
            // 06067412 46 00           shll       r6
            t = (r6 & 0x8000_0000) != 0; r6 <<= 1;
            // 06067414 42 24           rotcl      r2
            { var tmp = (r2 & 0x8000_0000) != 0; r2 <<= 1; if (t) { r2 |= 1; } else { r2 &= 0xfffffffe; } t = tmp; }
            // 06067416 4a 10           dt         r10
            r10--; t = r10 == 0;
            // 06067418 8b fb           bf         LAB_06067412
            if (!t) goto LAB_06067412;
            // 0606741a 32 0c           add        r0,r2
            r2 += r0;
        LAB_0606741c:
            // 0606741c 85 f0           mov.w      @(0x0,r15),r0
            r0 = (uint)(short)stack0;
            // 0606741e 00 cd           mov.w      @(r0,r12),r0
            r0 = (uint)(short)(r0 + r12).read16();
            // 06067420 81 f0           mov.w      r0,@(0x0,r15)
            stack0 = (ushort)r0;
            // 06067422 a0 34           bra        LAB_0606748e
            // 06067424 0b 25           _mov.w     r2,@(r0,r11=>DAT_060cb100)
            (r0 + r11).write16(r2);
            goto LAB_0606748e;
        LAB_06067426:
            // 06067426 85 f0           mov.w      @(0x0,r15),r0
            r0 = (uint)(short)stack0;
            // 06067428 61 03           mov        r0,r1
            r1 = r0;
            // 0606742a e0 08           mov        #0x8,r0
            r0 = 0x8;
            // 0606742c 37 02           cmp/hs     r0,r7
            t = r7 >= r0;
            // 0606742e 8b 0a           bf         LAB_06067446
            if (!t) goto LAB_06067446;
            // 06067430 60 63           mov        r6,r0
            r0 = r6;
            // 06067432 46 18           shll8      r6
            r6 <<= 8;
            // 06067434 40 29           shlr16     r0
            r0 >>= 16;
            // 06067436 40 19           shlr8      r0
            r0 >>= 8;
            // 06067438 77 f8           add        -0x8,r7
            r7 -= 8;
            // 0606743a 27 78           tst        r7,r7
            t = (r7 & r7) == 0;
            // 0606743c 8f 11           bf/s       LAB_06067462
            // 0606743e c9 7f           _and       0x7f,r0
            if (!t) { r0 &= 0x7f; goto LAB_06067462; } else { r0 &= 0x7f; }
            // 06067440 e7 20           mov        #32,r7
            r7 = 32;
            // 06067442 a0 0e           bra        LAB_06067462
            // 06067444 66 e6           _mov.l     @r14+,r6
            r6 = r14.read32(); r14 += 4;
            goto LAB_06067462;
        LAB_06067446:
            // 06067446 46 29           shlr16     r6
            r6 >>= 16;
            // 06067448 46 19           shlr8      r6
            r6 >>= 8;
            // 0606744a 60 63           mov        r6,r0
            r0 = r6;
            // 0606744c 66 e6           mov.l      @r14+,r6
            r6 = r14.read32(); r14 += 4;
            // 0606744e c9 7f           and        0x7f,r0
            r0 &= 0x7f;
            // 06067450 6a 7b           neg        r7,r10
            r10 = 0 - r7;
            // 06067452 7a 08           add        0x8,r10
            r10 += 0x8;
            // 06067454 77 18           add        24,r7
            r7 += 24;
            // 06067456 e2 00           mov        #0x0,r2
            r2 = 0;
        LAB_06067458:
            // 06067458 46 00           shll       r6
            t = (r6 & 0x8000_0000) != 0; r6 <<= 1;
            // 0606745a 42 24           rotcl      r2
            { var tmp = (r2 & 0x8000_0000) != 0; r2 <<= 1; if (t) { r2 |= 1; } else { r2 &= 0xfffffffe; } t = tmp; }
            // 0606745c 4a 10           dt         r10
            r10--; t = r10 == 0;
            // 0606745e 8b fb           bf         LAB_06067458
            if (!t) goto LAB_06067458;
            // 06067460 30 2c           add        r2,r0
            r0 += r2;
        LAB_06067462:
            // 06067462 40 00           shll       r0
            t = (r0 & 0x8000_0000) != 0; r0 <<= 1;
            // 06067464 6a 03           mov        r0,r10
            r10 = r0;
            // 06067466 30 10           cmp/eq     r1,r0
            t = r0 == r1;
            // 06067468 89 10           bt         LAB_0606748c
            if (t) goto LAB_0606748c;
            // 0606746a 02 dd           mov.w      @(r0,r13),r2
            r2 = (uint)(short)(r0 + r13).read16();
            // 0606746c 01 cd           mov.w      @(r0,r12),r1
            r1 = (uint)(short)(r0 + r12).read16();
            // 0606746e 60 23           mov        r2,r0
            r0 = r2;
            // 06067470 0c 15           mov.w      r1,@(r0,r12)
            (r0 + r12).write16(r1);
            // 06067472 60 13           mov        r1,r0
            r0 = r1;
            // 06067474 0d 25           mov.w      r2,@(r0,r13)
            (r0 + r13).write16(r2);
            // 06067476 85 f0           mov.w      @(0x0,r15),r0
            r0 = (uint)(short)stack0;
            // 06067478 61 03           mov        r0,r1
            r1 = r0;
            // 0606747a 02 cd           mov.w      @(r0,r12),r2
            r2 = (uint)(short)(r0 + r12).read16();
            // 0606747c 0c a5           mov.w      r10,@(r0,r12)
            (r0 + r12).write16(r10);
            // 0606747e 60 a3           mov        r10,r0
            r0 = r10;
            // 06067480 0c 25           mov.w      r2,@(r0,r12)
            (r0 + r12).write16(r2);
            // 06067482 60 23           mov        r2,r0
            r0 = r2;
            // 06067484 0d a5           mov.w      r10,@(r0,r13)
            (r0 + r13).write16(r10);
            // 06067486 60 a3           mov        r10,r0
            r0 = r10;
            // 06067488 0d 15           mov.w      r1,@(r0,r13)
            (r0 + r13).write16(r1);
            // 0606748a 81 f0           mov.w      r0,@(0x0,r15)
            stack0 = (ushort)r0;
        LAB_0606748c:
            // 0606748c 02 bd           mov.w      @(r0,r11=>DAT_060cb100),r2
            r2 = (uint)(short)(r0 + r11).read16();
        LAB_0606748e:
            // 0606748e 29 21           mov.w      r2,@r9
            r9.write16(r2);
            // 06067490 6a 93           mov        r9,r10
            r10 = r9;
            // 06067492 79 02           add        0x2,r9
            r9 += 0x2;
            // 06067494 47 10           dt         r7
            r7--; t = r7 == 0;
            // 06067496 8f 02           bf/s       LAB_0606749e
            // 06067498 46 00           _shll      r6
            if (!t) { t = (r6 & 0x8000_0000) != 0; r6 <<= 1; goto LAB_0606749e; } else { t = (r6 & 0x8000_0000) != 0; r6 <<= 1; }
            // 0606749a 66 e6           mov.l      @r14+,r6
            r6 = r14.read32(); r14 += 4;
            // 0606749c e7 20           mov        #0x20,r7
            r7 = 0x20;
        LAB_0606749e:
            // 0606749e 8b 28           bf         LAB_060674f2
            if (!t) goto LAB_060674f2;
            // 060674a0 61 27           not        r2,r1
            r1 = ~r2;
        LAB_060674a2:
            // 060674a2 e0 00           mov        #0x0,r0
            r0 = 0;
            // 060674a4 47 10           dt         r7
            r7--; t = r7 == 0;
            // 060674a6 8f 02           bf/s       LAB_060674ae
            // 060674a8 46 00           _shll      r6
            if (!t) { t = (r6 & 0x8000_0000) != 0; r6 <<= 1; goto LAB_060674ae; } else { t = (r6 & 0x8000_0000) != 0; r6 <<= 1; }
            // 060674aa 66 e6           mov.l      @r14+,r6
            r6 = r14.read32(); r14 += 4;
            // 060674ac e7 20           mov        #0x20,r7
            r7 = 0x20;
        LAB_060674ae:
            // 060674ae 40 24           rotcl      r0
            { var tmp = (r0 & 0x8000_0000) != 0; r0 <<= 1; if (t) { r0 |= 1; } else { r0 &= 0xfffffffe; } t = tmp; }
            // 060674b0 47 10           dt         r7
            r7--; t = r7 == 0;
            // 060674b2 8f 02           bf/s       LAB_060674ba
            // 060674b4 46 00           _shll      r6
            if (!t) { t = (r6 & 0x8000_0000) != 0; r6 <<= 1; goto LAB_060674ba; } else { t = (r6 & 0x8000_0000) != 0; r6 <<= 1; }
            // 060674b6 66 e6           mov.l      @r14+,r6
            r6 = r14.read32(); r14 += 4;
            // 060674b8 e7 20           mov        #0x20,r7
            r7 = 0x20;
        LAB_060674ba:
            // 060674ba 40 24           rotcl      r0
            { var tmp = (r0 & 0x8000_0000) != 0; r0 <<= 1; if (t) { r0 |= 1; } else { r0 &= 0xfffffffe; } t = tmp; }
            // 060674bc 20 08           tst        r0,r0
            t = (r0 & r0) == 0;
            // 060674be 89 05           bt         LAB_060674cc
            if (t) goto LAB_060674cc;
            // 060674c0 40 00           shll       r0
            t = (r0 & 0x8000_0000) != 0; r0 <<= 1;
            // 060674c2 70 fc           add        -0x4,r0
            r0 -= 4;
            // 060674c4 3a 0c           add        r0,r10
            r10 += r0;
            // 060674c6 3a 8c           add        r8,r10
            r10 += r8;
            // 060674c8 af eb           bra        LAB_060674a2
            // 060674ca 2a 11           _mov.w     r1,@r10=>DAT_00000400
            r10.write16(r1);
            goto LAB_060674a2;
        LAB_060674cc:
            // 060674cc 47 10           dt         r7
            r7--; t = r7 == 0;
            // 060674ce 8f 02           bf/s       LAB_060674d6
            // 060674d0 46 00           _shll      r6
            if (!t) { t = (r6 & 0x8000_0000) != 0; r6 <<= 1; goto LAB_060674d6; } else { t = (r6 & 0x8000_0000) != 0; r6 <<= 1; }
            // 060674d2 66 e6           mov.l      @r14+,r6
            r6 = r14.read32(); r14 += 4;
            // 060674d4 e7 20           mov        #0x20,r7
            r7 = 0x20;
        LAB_060674d6:
            // 060674d6 8b 0c           bf         LAB_060674f2
            if (!t) goto LAB_060674f2;
            // 060674d8 47 10           dt         r7
            r7--; t = r7 == 0;
            // 060674da 8f 02           bf/s       LAB_060674e2
            // 060674dc 46 00           _shll      r6
            if (!t) { t = (r6 & 0x8000_0000) != 0; r6 <<= 1; goto LAB_060674e2; } else { t = (r6 & 0x8000_0000) != 0; r6 <<= 1; }
            // 060674de 66 e6           mov.l      @r14+,r6
            r6 = r14.read32(); r14 += 4;
            // 060674e0 e7 20           mov        #0x20,r7
            r7 = 0x20;
        LAB_060674e2:
            // 060674e2 8d 03           bt/s       LAB_060674ec
            // 060674e4 3a 8c           _add       r8,r10
            if (t) { r10 += r8; goto LAB_060674ec; } else { r10 += r8; }
            // 060674e6 7a fc           add        -0x4,r10
            r10 -= 4;
            // 060674e8 af db           bra        LAB_060674a2
            // 060674ea 2a 11           _mov.w     r1,@r10
            r10.write16(r1);
            goto LAB_060674a2;
        LAB_060674ec:
            // 060674ec 7a 04           add        0x4,r10
            r10 += 0x4;
            // 060674ee af d8           bra        LAB_060674a2
            // 060674f0 2a 11           _mov.w     r1,@r10=>DAT_00000404
            r10.write16(r1);
            goto LAB_060674a2;
        LAB_060674f2:
            // 060674f2 b0 37           bsr        GetBitRun
            // 060674f4 00 09           _nop
            GetBitRun();
            // 060674f6 24 48           tst        r4,r4
            t = (r4 & r4) == 0;
            // 060674f8 89 04           bt         LAB_06067504
            if (t) goto LAB_06067504;
            // 060674fa af 61           bra        LAB_060673c0
            // 060674fc 00 09           _nop
            goto LAB_060673c0;
        LAB_060674fe:
            // 060674fe b0 23           bsr        FlipWords
            // 06067500 65 43           _mov       r4,r5
            r5 = r4;
            FlipWords();
            // 06067502 33 48           sub        r4,r3
            r3 -= r4;
        LAB_06067504:
            // 06067504 85 f1           mov.w      @(0x2,r15),r0
            r0 = (uint)(short)stack2;
            // 06067506 70 ff           add        -0x1,r0
            r0--;
            // 06067508 40 15           cmp/pl     r0
            t = (int)r0 > 0;
            // 0606750a 8b 01           bf         LAB_06067510
            if (!t) goto LAB_06067510;
            // 0606750c af 51           bra        LAB_060673b2
            // 0606750e 00 09           _nop
            goto LAB_060673b2;
        LAB_06067510:
            // 06067510 85 f2           mov.w      @(0x4,r15),r0
            r0 = (uint)(short)stack4;
            // 06067512 c8 02           tst        0x2,r0
            t = (r0 & 0x2) == 0;
            // 06067514 89 02           bt         LAB_0606751c
            if (t) goto LAB_0606751c;
            // 06067516 b0 67           bsr        FillBits2
            // 06067518 00 09           _nop
            void FillBits2()
            {
                // 060675e8 51 f4           mov.l      @(0x10,r15),r1
                r1 = stack10;
                // 060675ea d2 09           mov.l      @(INT_06067610,pc),r2                            = 8000h
                r2 = 0x8000;
                // 060675ec 53 f2           mov.l      @(0x8,r15),r3
                r3 = stack8;
                // 060675ee 73 ff           add        -0x1,r3
                r3--;
                // 060675f0 33 3c           add        r3,r3
                r3 += r3;
                // 060675f2 54 f3           mov.l      @(0xc,r15),r4
                r4 = stackc;
                // 060675f4 98 0b           mov.w      @(SHORT_0606760e,pc),r8                          = 400h
                r8 = (uint)(short)VRAM_WIDTH_BYTES;
            LAB_060675f6:
                // 060675f6 60 33           mov        r3,r0
                r0 = r3;
            LAB_060675f8:
                // 060675f8 05 1d           mov.w      @(r0,r1),r5
                r5 = (uint)(short)(r0 + r1).read16();
                // 060675fa 25 2b           or         r2,r5
                r5 |= r2;
                // 060675fc 01 55           mov.w      r5,@(r0,r1)
                (r0 + r1).write16(r5);
                // 060675fe 70 fe           add        -0x2,r0
                r0 -= 2;
                // 06067600 40 11           cmp/pz     r0
                t = (int)r0 >= 0;
                // 06067602 89 f9           bt         LAB_060675f8
                if (t) goto LAB_060675f8;
                // 06067604 31 8c           add        r8,r1
                r1 += r8;
                // 06067606 44 10           dt         r4
                r4--; t = r4 == 0;
                // 06067608 8b f5           bf         LAB_060675f6
                if (!t) goto LAB_060675f6;
                // 0606760a 00 0b           rts
                // 0606760c 00 09           _nop
                return;
            }
            FillBits2();
            // 0606751a a0 03           bra        LAB_06067524
            // LAB_0606751c
            // 0606751c c8 04           _tst       0x4,r0
            t = (r0 & 0x4) == 0;
            goto LAB_06067524;
        LAB_0606751c:
            t = (r0 & 0x4) == 0;
            // 0606751e 89 01           bt         LAB_06067524
            if (t) goto LAB_06067524;
            // 06067520 b0 78           bsr        FillBits1
            // 06067522 00 09           _nop
            void FillBits1()
            {
                // 06067614 51 f4           mov.l      @(0x10,r15),r1
                r1 = stack10;
                // 06067616 d2 0a           mov.l      @(INT_06067640,pc),r2                            = 8000h
                r2 = 0x8000;
                // 06067618 53 f2           mov.l      @(0x8,r15),r3
                r3 = stack8;
                // 0606761a 73 ff           add        -0x1,r3
                r3--;
                // 0606761c 33 3c           add        r3,r3
                r3 += r3;
                // 0606761e 54 f3           mov.l      @(0xc,r15),r4
                r4 = stackc;
                // 06067620 98 0d           mov.w      @(DAT_0606763e,pc),r8                            = 0400h
                r8 = (uint)(short)VRAM_WIDTH_BYTES;
            LAB_06067622:
                // 06067622 60 33           mov        r3,r0
                r0 = r3;
            LAB_06067624:
                // 06067624 05 1d           mov.w      @(r0,r1),r5
                r5 = (uint)(short)(r0 + r1).read16();
                // 06067626 25 58           tst        r5,r5
                t = (r5 & r5) == 0;
                // 06067628 89 01           bt         LAB_0606762e
                if (t) goto LAB_0606762e;
                // 0606762a 25 2b           or         r2,r5
                r5 |= r2;
                // 0606762c 01 55           mov.w      r5,@(r0,r1)
                (r0 + r1).write16(r5);
            LAB_0606762e:
                // 0606762e 40 15           cmp/pl     r0
                t = (int)r0 > 0;
                // 06067630 8d f8           bt/s       LAB_06067624
                // 06067632 70 fe           _add       -0x2,r0
                if (t) { r0 -= 2; goto LAB_06067624; } else { r0 -= 2; }
                // 06067634 44 10           dt         r4
                r4--; t = r4 == 0;
                // 06067636 8f f4           bf/s       LAB_06067622
                // 06067638 31 8c           _add       r8,r1
                if (!t) { r1 += r8; goto LAB_06067622; } else { r1 += r8; }
                // 0606763a 00 0b           rts
                // 0606763c 00 09           _nop
            }
            FillBits1();
        LAB_06067524:
            return (dataWidth, dataHeight);
        }

    }

    static class Extensions
    {
        static bool IsBetween(this uint ptr, uint lowInclusive, uint highExclusive) =>
            (ptr >= lowInclusive) &&
            (ptr < highExclusive);

        static bool IsBetween(this ulong ptr, ulong lowInclusive, ulong highExclusive) =>
            (ptr >= lowInclusive) &&
            (ptr < highExclusive);

        static IntPtr TranslateAddress(uint ptr)
        {
            try
            {
                if (ptr.IsBetween(0x0000_0000, 0x001F_FFFF))
                {
                    return Program.FileBuffer + (int)ptr;
                }
                if (ptr.IsBetween(0x05E0_0000, 0x05E7_FFFF))
                {
                    return Program.VRAM + (int)ptr - 0x05E0_0000;
                }
                if (ptr.IsBetween(0x0600_0000, 0x06FF_FFFF))
                {
                    return Program.TempBuffer + (int)ptr - (int)Program.TMP_BUFFER_PTR;
                }
                throw new NotImplementedException();
            }
            catch (NotImplementedException nie)
            {
                var errBuilder = new StringBuilder();
                errBuilder.AppendLine($"Attempted address: {ptr:X16}");
                errBuilder.AppendLine($"Offending object: {nie.Source}");
                errBuilder.AppendLine($"Target site: {nie.TargetSite}");
                errBuilder.AppendLine($"{nie.StackTrace}");
                Console.WriteLine("ERROR: Decompressor attmpted to access invalid memory.");
                Console.WriteLine("Information about the error has been written to neid_error.txt");
                Program.Cleanup();
                Environment.Exit(0);
                return IntPtr.Zero; // technically unnecessary but the compiler complains without it
            }
        }

        public static ushort SwapBytes(ushort s)
        {
            return (ushort)(((s >> 8) & 0xff) | ((s & 0xff) << 8));
        }

        public static uint SwapBytes(uint i)
        {
            return
            ((i >> 24) & 0xff) |
            ((i >> 8) & 0x0000_ff00) |
            ((i << 8) & 0x00ff_0000) |
            (i << 24);
        }

        public static ushort read16(this uint ptr)
        {
            return SwapBytes((ushort)Marshal.ReadInt16(TranslateAddress(ptr)));
        }

        public static void write16(this uint ptr, uint val, bool flippedBits = false)
        {
            Marshal.WriteInt16(TranslateAddress(ptr), (short)SwapBytes((ushort)val));
        }

        public static uint read32(this uint ptr)
        {
            return SwapBytes((uint)Marshal.ReadInt32(TranslateAddress(ptr)));
        }
    }
}
