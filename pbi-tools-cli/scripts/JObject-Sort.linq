<Query Kind="Statements">
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <Namespace>Newtonsoft.Json.Linq</Namespace>
</Query>

var json = new JObject {
	{ "foo", "bar"},
	{ "a", new JArray {
		new JObject {
			{ "ZZ", null },
			{ "1", 1 },
			{ "b", new JObject {
				{ "t", "t" },
				{ "A", "A" }
			}}
		},
		42,
		null
	} }
};

json.ToString().Dump("Before");

JObject Sort(JObject j)
{
	var prop = j.Properties().ToList();
	j.RemoveAll();
	foreach (var property in prop.OrderBy(p => p.Name))
	{
		j.Add(property.Name, SortToken(property.Value));
	}
	return j;
};

JToken SortToken(JToken t)
{
	if (t is JObject obj)
		return Sort(obj);
	else if (t is JArray arr)
		return new JArray(arr.Select(x => SortToken(x)));
	else 
		return t;
};

Sort(json).ToString().Dump("After");
