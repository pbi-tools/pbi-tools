<Query Kind="Statements">
  <NuGetReference>Mono.Cecil</NuGetReference>
  <Namespace>Mono.Cecil</Namespace>
</Query>

var dll = Path.Combine(Util.CurrentQueryPath, @"..\..\.build\out\pbix-tools.exe");
var module = Mono.Cecil.ModuleDefinition.ReadModule(dll);//.Dump();

/* Handle TypeDefinition:
   - Fields
   - Properties
   - NestedTypes
   - Methods
   - Interfaces
   - BaseType
   
   - Method, Parameter
 */

/* Table: | OwnType          | Location             | PBI-Assembly                      | PBI-Type                                         | PBI-Location               |
          | ---------------- | -------------------- | --------------------------------- | ------------------------------------------------ | -------------------------- |
          | MashupSerializer | Method: ConvertEntry | Microsoft.Mashup.Client.Packaging | SerializationObjectModel.SerializedMetadataEntry | get_StringValue() : string |  */
		  
void CheckInterface(InterfaceImplementation interfaceImpl, TypeDefinition typeDef)
{ }

void CheckMethodParameterType(MethodDefinition origin, ParameterDefinition parameter)
{
	var typeRef = parameter.ParameterType;
	var name = typeRef.Scope.Name;
	if (name.StartsWith("Microsoft.PowerBI")
		|| name.StartsWith("Microsoft.Mashup"))
	{
		$"  [Parameter] {origin.Name} {parameter} : {typeRef}".Dump();
	}
}

void CheckMethodReturnType(MethodDefinition origin, TypeReference typeRef)
{
	var name = typeRef.Scope.Name;
	if (name.StartsWith("Microsoft.PowerBI")
		|| name.StartsWith("Microsoft.Mashup"))
	{
		$"  [ReturnType] {origin.Name} : {typeRef}".Dump();
	}
}

void CheckType<T>(T origin, TypeReference typeRef, object reference = null) where T : IMemberDefinition
{
	if (typeRef.Scope.Name.StartsWith("Microsoft.PowerBI")
		|| typeRef.Scope.Name.StartsWith("Microsoft.Mashup"))
	{
		$"  [{origin.GetType().Name.Replace("Definition", "")}] {origin.Name} : {reference ?? typeRef}".Dump();
	}
}

void ProcessType(TypeDefinition typeDef)
 {
 	typeDef.FullName.Dump();
	
 	// Fields
	foreach (var f in typeDef.Fields)
	{
		CheckType(f, f.FieldType);
	}

	// Properties
	foreach (var p in typeDef.Properties)
	{
		CheckType(p, p.PropertyType);
	}
	
	// NestedTypes
	foreach (var tt in typeDef.NestedTypes)
	{
		ProcessType(tt);
	}
	
	foreach (var i in typeDef.Interfaces)
	{
//		CheckType(i.InterfaceType, i);
	}
	
//	typeDef.BaseType
	
	// Methods
	foreach (var m in typeDef.Methods)
	{		
		// returntype
		CheckMethodReturnType(m, m.ReturnType);
		
		// Parameters
		foreach (var p in m.Parameters)
		{
			CheckMethodParameterType(m, p);
		}
		
		if (m.HasBody)
		{
			foreach (var instr in m.Body.Instructions)
			{
				if (instr.Operand is MethodReference _m)
				{
					CheckType(m, _m.DeclaringType, _m);
				}
			}
		}
	}
}

foreach (var t in module.Types.Where(t => t.FullName.StartsWith("PbixTools")))
{
	ProcessType(t);
}