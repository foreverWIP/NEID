# NEID
This program decompresses data found in the ENDING*.bin files in NiGHTS into Dreams.
The decompression works perfectly, however image output is not yet perfect.
For this reason the program also outputs the raw decompressed data to be examined.

## Usage
`./NEID <input file path> <offset to read data from, in hex or decimal>`

## To build
- [Install `dotnet`](https://dotnet.microsoft.com/en-us/download/dotnet/5.0)
- `git clone https://github.com/foreverWIP/NEID`
- `cd NEID`
- `dotnet build`