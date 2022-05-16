# BedrockWire

Proxy and packet inspection tool for Minecraft: Bedrock Edition.

It includes a proxy for live recording of communication between a Minecraft Bedrock client and server.  
It also allows you to save the recorded packets to disk and load them afterwards.

The core of the tool is the Minecraft Bedrock protocol specification that can be found [here](MCPEProtocol.xml).  
The protocol can be dynamically loaded and unloaded from inside the tool. The main idea is that you can change the protocol definition on the fly.  

In addition to the main GUI tool there are also two CLI tools:
- AuthDumpCli - Allows you to generate an Xbox authentication JSON file.
- ProxyCli - Allows you to start the proxy separately and dump the packets into a dump file.

## Protocol Specification Format
The protocol is specified in an XML file an example of which can be found [here](MCPEProtocol.xml).  
More documentation about the format can be found [here](PROTOCOL_SPEC.md).

## Packet Dump Format
The packet dump file format has a simple 4 byte header of which the first 3 bytes are a magic value `BDW`.  
The forth byte is the version. The rest of the format depends on the version.

Current format version is `1`.  
In version 1, after the header is a deflate compressed sequence of packets.  
Each packet is defined with the following sequence of fields:
 - Direction: `byte` - Direction of the packet, 0 for server-bound and 1 for client-bound.
 - Id: `byte` - Id of the Minecraft packet.
 - Time: `ulong` - Time in milliseconds when the packet was recorded relative to the start of recording.
 - Length: `uint` - Byte length of the packet payload.
 - Payload: `bytes` - Payload of the packet.

## Modules

The project has been modularized into 9 different modules:
- **BedrockWire (APP)** - The main GUI app. 
- **BedrockWireProxyCli (APP)** - The proxy in form of a CLI app.
- **BedrockWireAuthDumpCli (APP)** - Utility to generate Xbox auth tokens using a CLI app.


- **BedrockWireCore (LIB)** - Core protocol definition and packet decoding library.
- **BedrockWireAuthDump (LIB)** - Xbox authentication library.
- **BedrockWireFormat (LIB)** - Packet dump disk format library.
- **BedrockWireProxy (LIB)** - Proxy library.
- **BedrockWireProxyInterface (LIB)** 
- **MinetRaknet (LIB)** - RakNet implementation from [MiNET](https://github.com/NiclasOlofsson/MiNET/).

## Known issues
- Xbox authentication expires after 24 hours and refreshing automatically doesn't work.
- Protocol Specification is not complete. If you have a badly decoded packet, please create a GitHub issue with the packet dump containing it.

## Contributing
All contributions are welcome. Feel free to submit a Pull Request.