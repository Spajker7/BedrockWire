## Protocol Specification Format
The protocol is specified in an XML file an example of which can be found [here](MCPE%20Protocol.xml).  
The root of the format looks like this:  
```xml
<protocol gameVersion="1.18.30" protocolVersion="503">
    <types>
        <type name="someName">
            <field name="Item1" type="float" />
        </type>
    </types>
    <packets>
        <packet id="0x01" name="MCPE_LOGIN">
            <field name="Protocol Version" type="intBE" />
            <field name="Ignored" type="unsignedVarInt" />
        </packet>
        <!-- 
            ... 
        -->
    </packets>
</protocol>
```

The top level `<protocol>` element contains two elements `<types>` and `<packets>`.

`<types>` is a list of types which can then be used by other types or fields inside packets.  
Definition of types is the same as definition of packets and is explained further down.


`<packets>` element contains a list of `<packet>` elements where each element represents a single packet.  
Each packet needs to have `id` and `name` attributes.

To define contents of packets you need to use fields.

### Value fields

The simplest field is defined using the `<field>` element and contains two attributes: `name` and `type`  
Name can be any non-empty string, while the type needs to be either one of base supported types or complex types defined under the `<types>` element earlier.

The base types currently supported are:
- `shortBE` - big endian short
- `short` - little endian short
- `unsignedShort` - Unsigned little endian short
- `intBE` - big endian int
- `int` - little endian int
- `unsignedInt` - unsigned little endian int
- `longBE` - big endian long
- `long` - little endian long
- `float` - little endian float
- `rotationFloat` - 1 byte float scaled to 0-360
- `bool` - bool
- `string` - length prefixed string
- `byte` - byte
- `unsignedVarInt` - unsigned variable int
- `varInt` - variable int
- `unsignedVarLong` - unsigned variable long
- `varLong` - variable long
- `json` - length prefixed json string
- `jsonJwtArrayChain` - json chain of JWTs
- `jwt` - single jwt
- `uuid` - UUID
- `nbt` - NBT
- `byteArray` - length prefixed byte array
- `nbtSequence` - sequence of "bare" NBT elements HACK: only used in EventPacket
- `doubleVarIntProduct` - product of 2 variable ints HACK: only used in shaped recipes
- `fixed256` - 256 byte long byte array HACK: only used for heightmaps

### Control fields

In addition to these "simple" value fields there are several special types of fields known as control fields.  
These control fields change the decoding of the packet based on some earlier read value.

By default all control fields reference the value of the field before them.  
To reference an earlier field you can use the `refId` attribute on a `<field>` element to assign it an id.  
Then you can use the `ref` attribute on a control field to make it reference the earlier element.  
**Note:** References can only be used on the same element _level_.

They are:
 - `<conditional>`  
Conditional fields will read their subfields only if the condition set is true.  
The `condition` attribute is used to specify the expression, where `value` is the referenced value.  
Example:  
```xml
<field name="Count" type="varInt" />
<conditional condition="value >= 10">
    <field name="Name" type="string" />
</conditional>
``` 
- `<list>`  
  List fields will read their subfields N number of times based on the referenced value.  
  Example:
```xml
<field name="Count" type="varInt" />
<list>
    <field name="Name" type="string" />
</list>
``` 
- `<switch>`  
  Switch fields will read one of their `<case>` subfields if it's value equals the referenced value.  
  Example:
```xml
<field name="Type" type="byte" />
<switch>
    <case value="0">
      <field name="Data" type="string" />
    </case>
    <case value="1">
      <field name="Data" type="int" />
    </case>
    <case value="2">
      <field name="Data" type="float" />
    </case>
</switch>
``` 
- `<flags>`  
  Flags field is used for bit-wise flags.
  Example:
```xml
<field name="Flags" type="unsignedShort" />
<flags>
    <case value="0x01">
        <field name="X" type="float" />
    </case>
    <case value="0x02">
        <field name="X" type="float" />
    </case>
    <case value="0x04">
        <field name="X" type="float" />
    </case>
    <case value="0x08">
        <field name="Pitch" type="rotationFloat" />
    </case>
</flags>
``` 