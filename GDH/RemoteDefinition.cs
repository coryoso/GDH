using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GDH;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;
using Resthopper.IO;
using Rhino;
using Rhino.Geometry;
using Rhino.Runtime;
using Rhino.UI;

namespace GDH
{
	internal class RemoteDefinition : IDisposable
	{
		public enum PathType
		{
			GrasshopperDefinition,
			InternalizedDefinition,
			ComponentGuid,
			Server,
			NonresponsiveUrl,
			InvalidUrl
		}

		private GDHComponent _parentComponent;

		private Dictionary<string, Tuple<InputParamSchema, IGH_Param>> _inputParams;

		private Dictionary<string, IGH_Param> _outputParams;

		private string _description;

		private Bitmap _customIcon;

		private string _path;

		private string _cacheKey;

		public byte[] _internalizedDefinition;

		private const string _apiKeyName = "RhinoComputeKey";

		public PathType? _pathType;

		private static HttpClient _httpClient;

		private static List<IGH_Param> _params;

		public string Path
		{
			get
			{
				return _path;
			}
			set
			{
				_path = value;
			}
		}

		public byte[] InternalizedDefinition
		{
			get
			{
				return _internalizedDefinition;
			}
			set
			{
				_internalizedDefinition = value;
			}
		}

		public static HttpClient HttpClient
		{
			get
			{
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_0011: Expected O, but got Unknown
				if (_httpClient == null)
				{
					_httpClient = new HttpClient();
				}
				return _httpClient;
			}
		}

		public static RemoteDefinition Create(string path, GDHComponent parentComponent)
		{
			RemoteDefinition rc = new RemoteDefinition(HopsAppSettings.GoogleDrivePath + "/"+ path, parentComponent);
			if (path != null)
			{
				RemoteDefinitionCache.Add(rc);
			}
			return rc;
		}

		public void InternalizeDefinition(string path)
		{
			_internalizedDefinition = File.ReadAllBytes(path);
			_pathType = PathType.InternalizedDefinition;
			RemoteDefinitionCache.Remove(this);
			_path = null;
		}

		private RemoteDefinition(string path, GDHComponent parentComponent)
		{
			_parentComponent = parentComponent;
			_path = path;
			_internalizedDefinition = null;
		}

		public void Dispose()
		{
			_parentComponent = null;
			RemoteDefinitionCache.Remove(this);
		}

		public bool IsNotResponingUrl()
		{
			return GetPathType() == PathType.NonresponsiveUrl;
		}

		public bool IsInvalidUrl()
		{
			return GetPathType() == PathType.InvalidUrl;
		}

		public void ResetPathType()
		{
			_pathType = null;
		}

		private PathType GetPathType()
		{
			if (!_pathType.HasValue)
			{
				_pathType = GetPathType(_path);
			}
			return _pathType.Value;
		}

		public static PathType GetPathType(string path)
		{
			if (Guid.TryParse(path, out var _))
			{
				return PathType.ComponentGuid;
			}
			PathType rc = PathType.GrasshopperDefinition;
			if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					if (HttpClient.GetAsync(path).Result.Content.Headers.ContentType
						.MediaType
						.ToLowerInvariant()
						.Contains("json"))
					{
						return PathType.Server;
					}
					return rc;
				}
				catch (Exception)
				{
					return PathType.NonresponsiveUrl;
				}
			}
			return rc;
		}

		public void OnWatchedFileChanged()
		{
			_cacheKey = null;
			_description = null;
			if (_parentComponent != null)
			{
				_parentComponent.OnRemoteDefinitionChanged();
			}
		}

		public Dictionary<string, Tuple<InputParamSchema, IGH_Param>> GetInputParams()
		{
			if (_inputParams == null)
			{
				GetRemoteDescription();
			}
			return _inputParams;
		}

		public Dictionary<string, IGH_Param> GetOutputParams()
		{
			if (_outputParams == null)
			{
				GetRemoteDescription();
			}
			return _outputParams;
		}

		public string GetDescription(out Bitmap customIcon)
		{
			if (_description == null)
			{
				GetRemoteDescription();
			}
			customIcon = _customIcon;
			return _description;
		}

		public void GetRemoteDescription()
		{
			//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a7: Expected O, but got Unknown
			//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ae: Expected O, but got Unknown
			bool performPost = false;
			string address = null;
			PathType pathType = GetPathType();
			switch (pathType)
			{
				case PathType.GrasshopperDefinition:
					if (Path.StartsWith("http", StringComparison.OrdinalIgnoreCase) || File.Exists(Path))
					{
						address = Path;
						performPost = true;
					}
					break;
				case PathType.ComponentGuid:
					address = Servers.GetDescriptionUrl(Guid.Parse(Path));
					break;
				case PathType.InternalizedDefinition:
					address = "internalized";
					performPost = true;
					break;
				case PathType.Server:
					address = Path;
					break;
			}
			if (address == null)
			{
				return;
			}
			IoResponseSchema responseSchema = null;
			IDisposable contentToDispose = null;
			Task<HttpResponseMessage> responseTask;
			if (performPost)
			{
				string postUrl = Servers.GetDescriptionPostUrl();
				Schema schema = new Schema();
				if (pathType != PathType.InternalizedDefinition)
				{
					if (Path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
					{
						schema.Pointer = address;
					}
					else
					{
						byte[] bytes = File.ReadAllBytes(address);
						schema.Algo = Convert.ToBase64String(bytes);
					}
				}
				else if (_internalizedDefinition != null)
				{
					schema.Algo = Convert.ToBase64String(_internalizedDefinition);
				}
				schema.AbsoluteTolerance = GetDocumentTolerance();
				schema.AngleTolerance = GetDocumentAngleTolerance();
				schema.ModelUnits = GetDocumentUnits();
				string inputJson = JsonConvert.SerializeObject((object)schema);
				string requestContent2 = "{";
				requestContent2 = requestContent2 + "\"URL\": \"" + postUrl + "\"," + Environment.NewLine;
				requestContent2 = requestContent2 + "\"Method\": \"POST\"," + Environment.NewLine;
				requestContent2 = requestContent2 + "\"Content\": " + inputJson + Environment.NewLine;
				requestContent2 += "}";
				_parentComponent.HTTPRecord.IORequest = requestContent2;
				StringContent content = new StringContent(inputJson, Encoding.UTF8, "application/json");
				HttpClient client = new HttpClient();
				if (!string.IsNullOrEmpty(HopsAppSettings.APIKey))
				{
					((HttpHeaders)client.DefaultRequestHeaders).Add("RhinoComputeKey", HopsAppSettings.APIKey);
				}
				if (HopsAppSettings.HTTPTimeout > 0)
				{
					client.Timeout = (TimeSpan.FromSeconds(HopsAppSettings.HTTPTimeout));
				}
				responseTask = client.PostAsync(postUrl, (HttpContent)(object)content);
				_parentComponent.HTTPRecord.Schema = schema;
				contentToDispose = (IDisposable)content;
			}
			else
			{
				string requestContent = "{";
				requestContent = requestContent + "\"URL\": \"" + address + "\"," + Environment.NewLine;
				requestContent = requestContent + "\"Method\": \"GET\"" + Environment.NewLine;
				requestContent += "}";
				_parentComponent.HTTPRecord.IORequest = requestContent;
				responseTask = HttpClient.GetAsync(address);
			}
			if (responseTask != null)
			{
				string stringResult = responseTask.Result.Content.ReadAsStringAsync().Result;
				if (string.IsNullOrEmpty(stringResult))
				{
					_pathType = PathType.InvalidUrl;
					_parentComponent.HTTPRecord.IOResponse = "Invalid URL";
				}
				else
				{
					_parentComponent.HTTPRecord.IOResponse = stringResult;
					responseSchema = JsonConvert.DeserializeObject<IoResponseSchema>(stringResult);
					_cacheKey = responseSchema.CacheKey;
					_parentComponent.HTTPRecord.IOResponseSchema = responseSchema;
				}
			}
			contentToDispose?.Dispose();
			if (responseSchema == null)
			{
				return;
			}
			_description = responseSchema.Description;
			_customIcon = null;
			if (!string.IsNullOrWhiteSpace(responseSchema.Icon))
			{
				try
				{
					string svg = responseSchema.Icon;
					if (svg.IndexOf("svg", StringComparison.InvariantCultureIgnoreCase) < 0 || svg.IndexOf("xmlns", StringComparison.InvariantCultureIgnoreCase) < 0)
					{
						svg = null;
					}
					if (svg != null)
					{
						MethodInfo method = typeof(DrawingUtilities).GetMethod("BitmapFromSvg");
						if (method != null)
						{
							_customIcon = method.Invoke(null, new object[3] { svg, 24, 24 }) as Bitmap;
						}
					}
					if (_customIcon == null)
					{
						using MemoryStream ms = new MemoryStream(Convert.FromBase64String(responseSchema.Icon));
						_customIcon = new Bitmap(ms);
						if (_customIcon != null && (_customIcon.Width != 24 || _customIcon.Height != 24))
						{
							Bitmap temp = _customIcon;
							_customIcon = new Bitmap(temp, new Size(24, 24));
							temp.Dispose();
						}
					}
				}
				catch (Exception)
				{
				}
			}
			_inputParams = new Dictionary<string, Tuple<InputParamSchema, IGH_Param>>();
			_outputParams = new Dictionary<string, IGH_Param>();
			foreach (InputParamSchema input in responseSchema.Inputs)
			{
				string inputParamName = input.Name;
				if (inputParamName.StartsWith("RH_IN:"))
				{
					string[] array = inputParamName.Split(':');
					inputParamName = array[array.Length - 1];
				}
				_inputParams[inputParamName] = Tuple.Create<InputParamSchema, IGH_Param>(input, ParamFromIoResponseSchema(input));
			}
			foreach (IoParamSchema output in responseSchema.Outputs)
			{
				string outputParamName = output.Name;
				if (outputParamName.StartsWith("RH_OUT:"))
				{
					string[] array2 = outputParamName.Split(':');
					outputParamName = array2[array2.Length - 1];
				}
				_outputParams[outputParamName] = ParamFromIoResponseSchema(output);
			}
			_parentComponent.HTTPRecord.IOResponseSchema = responseSchema;
		}

		private double GetDocumentTolerance()
		{
			RhinoDoc rhinoDoc = RhinoDoc.ActiveDoc;
			if (rhinoDoc != null)
			{
				return rhinoDoc.ModelAbsoluteTolerance;
			}
			Type utilityType = typeof(Utility);
			if (utilityType == null)
			{
				return 0.0;
			}
			MethodInfo method = utilityType.GetMethod("DocumentTolerance", BindingFlags.Static | BindingFlags.Public);
			if (method == null)
			{
				return 0.0;
			}
			return (double)method.Invoke(null, null);
		}

		private double GetDocumentAngleTolerance()
		{
			RhinoDoc rhinoDoc = RhinoDoc.ActiveDoc;
			if (rhinoDoc != null)
			{
				return rhinoDoc.ModelAngleToleranceDegrees;
			}
			Type utilityType = typeof(Utility);
			if (utilityType == null)
			{
				return 0.0;
			}
			MethodInfo method = utilityType.GetMethod("DocumentAngleTolerance", BindingFlags.Static | BindingFlags.Public);
			if (method == null)
			{
				return 0.0;
			}
			return (double)method.Invoke(null, null);
		}

		private string GetDocumentUnits()
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			RhinoDoc rhinoDoc = RhinoDoc.ActiveDoc;
			if (rhinoDoc != null)
			{
				UnitSystem modelUnitSystem = rhinoDoc.ModelUnitSystem;
				return modelUnitSystem.ToString();
			}
			Type utilityType = typeof(Utility);
			if (utilityType == null)
			{
				return "";
			}
			MethodInfo method = utilityType.GetMethod("DocumentUnits", BindingFlags.Static | BindingFlags.Public);
			if (method == null)
			{
				return "";
			}
			return (string)method.Invoke(null, null);
		}

		private static Schema SafeSchemaDeserialize(string data)
		{
			try
			{
				return JsonConvert.DeserializeObject<Schema>(data);
			}
			catch (Exception)
			{
			}
			return null;
		}

		public Schema Solve(Schema inputSchema, bool useMemoryCache)
		{
			//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ff: Expected O, but got Unknown
			//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0106: Expected O, but got Unknown
			//IL_024a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0251: Expected O, but got Unknown
			//IL_0251: Unknown result type (might be due to invalid IL or missing references)
			//IL_0258: Expected O, but got Unknown
			string solveUrl;
			switch (GetPathType())
			{
				case PathType.NonresponsiveUrl:
					return null;
				case PathType.GrasshopperDefinition:
				case PathType.InternalizedDefinition:
				case PathType.ComponentGuid:
					solveUrl = Servers.GetSolveUrl();
					if (!string.IsNullOrEmpty(_cacheKey))
					{
						inputSchema.Pointer = _cacheKey;
					}
					break;
				default:
					{
						Path.LastIndexOf('/');
						string authority = new Uri(Path).Authority;
						solveUrl = "http://" + authority + "/solve";
						break;
					}
			}
			string inputJson = JsonConvert.SerializeObject((object)inputSchema);
			if (useMemoryCache && inputSchema.Algo == null)
			{
				Schema cachedResults = MemoryCache.Get(inputJson);
				if (cachedResults != null)
				{
					return cachedResults;
				}
			}
			string requestContent = "{";
			requestContent = requestContent + "\"URL\": \"" + solveUrl + "\"," + Environment.NewLine;
			requestContent = requestContent + "\"content\": " + inputJson + Environment.NewLine;
			requestContent += "}";
			_parentComponent.HTTPRecord.SolveRequest = requestContent;
			StringContent content = new StringContent(inputJson, Encoding.UTF8, "application/json");
			try
			{
				HttpClient client = new HttpClient();
				if (!string.IsNullOrEmpty(HopsAppSettings.APIKey))
				{
					((HttpHeaders)client.DefaultRequestHeaders).Add("RhinoComputeKey", HopsAppSettings.APIKey);
				}
				if (HopsAppSettings.HTTPTimeout > 0)
				{
					client.Timeout = (TimeSpan.FromSeconds(HopsAppSettings.HTTPTimeout));
				}
				HttpResponseMessage responseMessage = client.PostAsync(solveUrl, (HttpContent)(object)content).Result;
				string stringResult = responseMessage.Content.ReadAsStringAsync().Result;
				_parentComponent.HTTPRecord.SolveResponse = stringResult;
				Schema schema = SafeSchemaDeserialize(stringResult);
				if (schema == null && responseMessage.StatusCode == HttpStatusCode.InternalServerError)
				{
					bool fileExists = File.Exists(Path);
					if (fileExists && string.IsNullOrEmpty(inputSchema.Algo))
					{
						string base64 = (inputSchema.Algo = Convert.ToBase64String(File.ReadAllBytes(Path)));
						inputJson = JsonConvert.SerializeObject((object)inputSchema);
						requestContent = "{";
						requestContent = requestContent + "\"URL\": \"" + solveUrl + "\"," + Environment.NewLine;
						requestContent = requestContent + "\"content\":" + inputJson + Environment.NewLine;
						requestContent += "}";
						_parentComponent.HTTPRecord.SolveRequest = requestContent;
						StringContent content2 = new StringContent(inputJson, Encoding.UTF8, "application/json");
						HttpClient client2 = new HttpClient();
						if (!string.IsNullOrEmpty(HopsAppSettings.APIKey))
						{
							((HttpHeaders)client2.DefaultRequestHeaders).Add("RhinoComputeKey", HopsAppSettings.APIKey);
						}
						if (HopsAppSettings.HTTPTimeout > 0)
						{
							client2.Timeout = (TimeSpan.FromSeconds(HopsAppSettings.HTTPTimeout));
						}
						responseMessage = client.PostAsync(solveUrl, (HttpContent)(object)content2).Result;
						stringResult = responseMessage.Content.ReadAsStringAsync().Result;
						_parentComponent.HTTPRecord.SolveResponse = stringResult;
						schema = SafeSchemaDeserialize(stringResult);
						if (schema == null && responseMessage.StatusCode == HttpStatusCode.InternalServerError)
						{
							Schema badSchema3 = new Schema();
							badSchema3.Errors.Add("Unable to solve on compute");
							_parentComponent.HTTPRecord.Schema = badSchema3;
							return badSchema3;
						}
					}
					else if (!fileExists && string.IsNullOrEmpty(inputSchema.Algo) && GetPathType() == PathType.GrasshopperDefinition)
					{
						Schema badSchema2 = new Schema();
						badSchema2.Errors.Add("Unable to find file: " + Path);
						_parentComponent.HTTPRecord.Schema = badSchema2;
						return badSchema2;
					}
				}
				if (responseMessage.StatusCode == HttpStatusCode.RequestTimeout)
				{
					Schema badSchema = new Schema();
					badSchema.Errors.Add("Request timeout: " + Path);
					_parentComponent.HTTPRecord.Schema = badSchema;
					return badSchema;
				}
				bool rebuildDefinition = responseMessage.StatusCode == HttpStatusCode.InternalServerError && schema.Errors.Count > 0 && string.Equals(schema.Errors[0], "Bad inputs", StringComparison.OrdinalIgnoreCase);
				if (!rebuildDefinition && schema.Values.Count > 0 && schema.Values.Count != _outputParams.Count)
				{
					rebuildDefinition = true;
				}
				if (rebuildDefinition)
				{
					GetRemoteDescription();
					_parentComponent.OnRemoteDefinitionChanged();
				}
				else if (responseMessage.StatusCode == HttpStatusCode.OK && useMemoryCache && inputSchema.Algo == null)
				{
					MemoryCache.Set(inputJson, schema);
				}
				_cacheKey = schema.Pointer;
				return schema;
			}
			finally
			{
				((IDisposable)content)?.Dispose();
			}
		}

		public void SetComponentOutputs(Schema schema, IGH_DataAccess DA, List<IGH_Param> outputParams, GDHComponent component)
		{
			//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ba: Expected O, but got Unknown
			foreach (Resthopper.IO.DataTree<ResthopperObject> datatree in schema.Values)
			{
				string outputParamName = datatree.ParamName;
				if (outputParamName.StartsWith("RH_OUT:"))
				{
					string[] array = outputParamName.Split(':');
					outputParamName = array[array.Length - 1];
				}
				int paramIndex = 0;
				for (int j = 0; j < outputParams.Count; j++)
				{
					if (((IGH_InstanceDescription)outputParams[j]).Name.Equals(outputParamName))
					{
						paramIndex = j;
						break;
					}
				}
				GH_Structure<IGH_Goo> structure = new GH_Structure<IGH_Goo>();
				IGH_Goo goo = null;
				bool hasDataTreeAsInput = false;
				foreach (IGH_Param item in ((GH_Component)component).Params.Input)
				{
					if (item.VolatileData.PathCount > 1)
					{
						hasDataTreeAsInput = true;
						break;
					}
				}
				foreach (KeyValuePair<string, List<ResthopperObject>> kv in datatree.InnerTree)
				{
					string[] tokens = kv.Key.Trim('{', '}').Split(';');
					List<int> elements = new List<int>();
					if (datatree.InnerTree.Count == 1 && !hasDataTreeAsInput)
					{
						for (int i = 0; i < tokens.Length; i++)
						{
							if (i < tokens.Length - 1)
							{
								if (!string.IsNullOrWhiteSpace(tokens[i]))
								{
									elements.Add(int.Parse(tokens[i]));
								}
							}
							else
							{
								elements.Add(DA.Iteration);
							}
						}
					}
					else
					{
						string[] array2 = tokens;
						foreach (string token in array2)
						{
							if (!string.IsNullOrWhiteSpace(token))
							{
								elements.Add(int.Parse(token));
							}
						}
					}
					GH_Path path = new GH_Path(elements.ToArray());
					List<IGH_Goo> localBranch = structure.EnsurePath(path);
					for (int gooIndex = 0; gooIndex < kv.Value.Count; gooIndex++)
					{
						goo = GooFromReshopperObject(kv.Value[gooIndex]);
						localBranch.Add(goo);
					}
				}
				if (structure.DataCount == 1)
				{
					DA.SetData(paramIndex, (object)goo);
				}
				else
				{
					DA.SetDataTree(paramIndex, (IGH_Structure)(object)structure);
				}
			}
			foreach (string error in schema.Errors)
			{
				((GH_ActiveObject)component).AddRuntimeMessage((GH_RuntimeMessageLevel)20, error);
			}
			foreach (string warning in schema.Warnings)
			{
				((GH_ActiveObject)component).AddRuntimeMessage((GH_RuntimeMessageLevel)10, warning);
			}
		}

		private static IGH_Goo GooFromReshopperObject(ResthopperObject obj)
		{
			//IL_02bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c2: Expected O, but got Unknown
			//IL_02d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d8: Expected O, but got Unknown
			//IL_0311: Unknown result type (might be due to invalid IL or missing references)
			//IL_0318: Expected O, but got Unknown
			//IL_0329: Unknown result type (might be due to invalid IL or missing references)
			//IL_0330: Expected O, but got Unknown
			//IL_033c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0341: Unknown result type (might be due to invalid IL or missing references)
			//IL_0348: Expected O, but got Unknown
			//IL_0354: Unknown result type (might be due to invalid IL or missing references)
			//IL_0359: Unknown result type (might be due to invalid IL or missing references)
			//IL_0360: Expected O, but got Unknown
			//IL_036c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0371: Unknown result type (might be due to invalid IL or missing references)
			//IL_0378: Expected O, but got Unknown
			//IL_0384: Unknown result type (might be due to invalid IL or missing references)
			//IL_0389: Unknown result type (might be due to invalid IL or missing references)
			//IL_0390: Expected O, but got Unknown
			//IL_039c: Unknown result type (might be due to invalid IL or missing references)
			//IL_03a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_03a8: Expected O, but got Unknown
			//IL_03e6: Unknown result type (might be due to invalid IL or missing references)
			//IL_03ec: Expected O, but got Unknown
			//IL_03fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0402: Expected O, but got Unknown
			//IL_0412: Unknown result type (might be due to invalid IL or missing references)
			//IL_0418: Expected O, but got Unknown
			//IL_0428: Unknown result type (might be due to invalid IL or missing references)
			//IL_042e: Expected O, but got Unknown
			//IL_0445: Unknown result type (might be due to invalid IL or missing references)
			//IL_044b: Unknown result type (might be due to invalid IL or missing references)
			//IL_04da: Unknown result type (might be due to invalid IL or missing references)
			//IL_04e0: Expected O, but got Unknown
			//IL_04f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_04f6: Expected O, but got Unknown
			//IL_0506: Unknown result type (might be due to invalid IL or missing references)
			//IL_050c: Expected O, but got Unknown
			//IL_051c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0522: Expected O, but got Unknown
			if (obj.ResolvedData != null)
			{
				object resolvedData = obj.ResolvedData;
				return (IGH_Goo)((resolvedData is IGH_Goo) ? resolvedData : null);
			}
			string data = obj.Data.Trim('"');
			switch (obj.Type)
			{
				case "System.Boolean":
					return (IGH_Goo)(obj.ResolvedData = (object)new GH_Boolean(bool.Parse(data)));
				case "System.Double":
					return (IGH_Goo)(obj.ResolvedData = (object)new GH_Number(double.Parse(data)));
				case "System.String":
					{
						string unescaped = data;
						if (unescaped.Trim().StartsWith("{") && unescaped.Contains("\\"))
						{
							unescaped = Regex.Unescape(data);
						}
						return (IGH_Goo)(obj.ResolvedData = (object)new GH_String(unescaped));
					}
				case "System.Int32":
					return (IGH_Goo)(obj.ResolvedData = (object)new GH_Integer(int.Parse(data)));
				case "Rhino.Geometry.Circle":
					return (IGH_Goo)(obj.ResolvedData = (object)new GH_Circle(JsonConvert.DeserializeObject<Circle>(data)));
				case "Rhino.Geometry.Line":
					return (IGH_Goo)(obj.ResolvedData = (object)new GH_Line(JsonConvert.DeserializeObject<Line>(data)));
				case "Rhino.Geometry.Plane":
					return (IGH_Goo)(obj.ResolvedData = (object)new GH_Plane(JsonConvert.DeserializeObject<Plane>(data)));
				case "Rhino.Geometry.Point3d":
					return (IGH_Goo)(obj.ResolvedData = (object)new GH_Point(JsonConvert.DeserializeObject<Point3d>(data)));
				case "Rhino.Geometry.Vector3d":
					return (IGH_Goo)(obj.ResolvedData = (object)new GH_Vector(JsonConvert.DeserializeObject<Vector3d>(data)));
				case "Rhino.Geometry.Brep":
				case "Rhino.Geometry.Curve":
				case "Rhino.Geometry.Extrusion":
				case "Rhino.Geometry.Mesh":
				case "Rhino.Geometry.PolyCurve":
				case "Rhino.Geometry.NurbsCurve":
				case "Rhino.Geometry.PolylineCurve":
				case "Rhino.Geometry.SubD":
					{
						CommonObject geometry2 = CommonObject.FromJSON(JsonConvert.DeserializeObject<Dictionary<string, string>>(data));
						Surface surface2 = (Surface)(object)((geometry2 is Surface) ? geometry2 : null);
						if (surface2 != null)
						{
							geometry2 = (CommonObject)(object)surface2.ToBrep();
						}
						if (geometry2 is Brep)
						{
							return (IGH_Goo)new GH_Brep((Brep)(object)((geometry2 is Brep) ? geometry2 : null));
						}
						if (geometry2 is Curve)
						{
							return (IGH_Goo)new GH_Curve((Curve)(object)((geometry2 is Curve) ? geometry2 : null));
						}
						if (geometry2 is Mesh)
						{
							return (IGH_Goo)new GH_Mesh((Mesh)(object)((geometry2 is Mesh) ? geometry2 : null));
						}
						if (geometry2 is SubD)
						{
							return (IGH_Goo)new GH_SubD((SubD)(object)((geometry2 is SubD) ? geometry2 : null));
						}
						break;
					}
			}
			if (obj.Type.StartsWith("Rhino.Geometry"))
			{
				string s = ((object)default(Point3d)).GetType().AssemblyQualifiedName;
				int index = s.IndexOf(",");
				Type type = Type.GetType(obj.Type + s.Substring(index));
				if (type != null && typeof(GeometryBase).IsAssignableFrom(type))
				{
					CommonObject geometry = CommonObject.FromJSON(JsonConvert.DeserializeObject<Dictionary<string, string>>(data));
					Surface surface = (Surface)(object)((geometry is Surface) ? geometry : null);
					if (surface != null)
					{
						geometry = (CommonObject)(object)surface.ToBrep();
					}
					if (geometry is Brep)
					{
						return (IGH_Goo)new GH_Brep((Brep)(object)((geometry is Brep) ? geometry : null));
					}
					if (geometry is Curve)
					{
						return (IGH_Goo)new GH_Curve((Curve)(object)((geometry is Curve) ? geometry : null));
					}
					if (geometry is Mesh)
					{
						return (IGH_Goo)new GH_Mesh((Mesh)(object)((geometry is Mesh) ? geometry : null));
					}
					if (geometry is SubD)
					{
						return (IGH_Goo)new GH_SubD((SubD)(object)((geometry is SubD) ? geometry : null));
					}
				}
			}
			throw new Exception("unable to convert resthopper data");
		}

		private static IGH_Param ParamFromIoResponseSchema(IoParamSchema item)
		{
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Expected O, but got Unknown
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Expected O, but got Unknown
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Expected O, but got Unknown
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Expected O, but got Unknown
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Expected O, but got Unknown
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Expected O, but got Unknown
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Expected O, but got Unknown
			//IL_0082: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Expected O, but got Unknown
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_009b: Expected O, but got Unknown
			//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00aa: Expected O, but got Unknown
			//IL_00af: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b9: Expected O, but got Unknown
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c8: Expected O, but got Unknown
			//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d7: Expected O, but got Unknown
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e6: Expected O, but got Unknown
			//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f5: Expected O, but got Unknown
			//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_0104: Expected O, but got Unknown
			//IL_0109: Unknown result type (might be due to invalid IL or missing references)
			//IL_0113: Expected O, but got Unknown
			//IL_0118: Unknown result type (might be due to invalid IL or missing references)
			//IL_0122: Expected O, but got Unknown
			//IL_0127: Unknown result type (might be due to invalid IL or missing references)
			//IL_0131: Expected O, but got Unknown
			//IL_0136: Unknown result type (might be due to invalid IL or missing references)
			//IL_0140: Expected O, but got Unknown
			//IL_0145: Unknown result type (might be due to invalid IL or missing references)
			//IL_014f: Expected O, but got Unknown
			//IL_0154: Unknown result type (might be due to invalid IL or missing references)
			//IL_015e: Expected O, but got Unknown
			//IL_0163: Unknown result type (might be due to invalid IL or missing references)
			//IL_016d: Expected O, but got Unknown
			//IL_0172: Unknown result type (might be due to invalid IL or missing references)
			//IL_017c: Expected O, but got Unknown
			//IL_0181: Unknown result type (might be due to invalid IL or missing references)
			//IL_018b: Expected O, but got Unknown
			//IL_0190: Unknown result type (might be due to invalid IL or missing references)
			//IL_019a: Expected O, but got Unknown
			//IL_019f: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a9: Expected O, but got Unknown
			//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b8: Expected O, but got Unknown
			//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c7: Expected O, but got Unknown
			//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d6: Expected O, but got Unknown
			//IL_01db: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e5: Expected O, but got Unknown
			//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f4: Expected O, but got Unknown
			//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0203: Expected O, but got Unknown
			//IL_0208: Unknown result type (might be due to invalid IL or missing references)
			//IL_0212: Expected O, but got Unknown
			if (_params == null)
			{
				_params = new List<IGH_Param>();
				_params.Add((IGH_Param)new Param_Arc());
				_params.Add((IGH_Param)new Param_Boolean());
				_params.Add((IGH_Param)new Param_Box());
				_params.Add((IGH_Param)new Param_Brep());
				_params.Add((IGH_Param)new Param_Circle());
				_params.Add((IGH_Param)new Param_Colour());
				_params.Add((IGH_Param)new Param_Complex());
				_params.Add((IGH_Param)new Param_Culture());
				_params.Add((IGH_Param)new Param_Curve());
				_params.Add((IGH_Param)new Param_Field());
				_params.Add((IGH_Param)new Param_GenericObject());
				_params.Add((IGH_Param)new Param_Geometry());
				_params.Add((IGH_Param)new Param_Group());
				_params.Add((IGH_Param)new Param_Guid());
				_params.Add((IGH_Param)new Param_Integer());
				_params.Add((IGH_Param)new Param_Interval());
				_params.Add((IGH_Param)new Param_Interval2D());
				_params.Add((IGH_Param)new Param_LatLonLocation());
				_params.Add((IGH_Param)new Param_Line());
				_params.Add((IGH_Param)new Param_Matrix());
				_params.Add((IGH_Param)new Param_Mesh());
				_params.Add((IGH_Param)new Param_MeshFace());
				_params.Add((IGH_Param)new Param_MeshParameters());
				_params.Add((IGH_Param)new Param_Number());
				_params.Add((IGH_Param)new Param_Plane());
				_params.Add((IGH_Param)new Param_Point());
				_params.Add((IGH_Param)new Param_Rectangle());
				_params.Add((IGH_Param)new Param_String());
				_params.Add((IGH_Param)new Param_StructurePath());
				_params.Add((IGH_Param)new Param_SubD());
				_params.Add((IGH_Param)new Param_Surface());
				_params.Add((IGH_Param)new Param_Time());
				_params.Add((IGH_Param)new Param_Transform());
				_params.Add((IGH_Param)new Param_Vector());
			}
			foreach (IGH_Param p in _params)
			{
				if (!p.TypeName.Equals(item.ParamType, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				object obj = Activator.CreateInstance(((object)p).GetType());
				IGH_Param ghParam = (IGH_Param)((obj is IGH_Param) ? obj : null);
				if (ghParam != null)
				{
					string name = item.Name;
					if (!string.IsNullOrEmpty(name))
					{
						if (name.StartsWith("RH_IN:"))
						{
							name = name.Substring("RH_IN:".Length).Trim();
						}
						if (name.StartsWith("RH_OUT:"))
						{
							name = name.Substring("RH_OUT:".Length).Trim();
						}
					}
					if (!string.IsNullOrEmpty(name))
					{
						((IGH_InstanceDescription)ghParam).Name = item.Name;
					}
					string nickname = name;
					if (!string.IsNullOrEmpty(item.Nickname))
					{
						nickname = item.Nickname;
					}
					if (!string.IsNullOrEmpty(nickname))
					{
						((IGH_InstanceDescription)ghParam).NickName= nickname;
					}
				}
				return ghParam;
			}
			return null;
		}

		private static bool CheckMinMax<T>(T item, string name, InputParamSchema schema, ref List<string> errors)
		{
			if (schema.Minimum != null)
			{
				try
				{
					if (Convert.ToDouble(item) < Convert.ToDouble(schema.Minimum))
					{
						errors.Add($"{name} value must be greater than the specified minimum value of the parameter");
						return false;
					}
				}
				catch (Exception ex)
				{
					errors.Add(ex.ToString());
					return false;
				}
			}
			if (schema.Maximum != null)
			{
				try
				{
					if (Convert.ToDouble(item) > Convert.ToDouble(schema.Maximum))
					{
						errors.Add($"{name} value must be smaller than the specified maximum value of the parameter");
						return false;
					}
				}
				catch (Exception ex2)
				{
					errors.Add(ex2.ToString());
					return false;
				}
			}
			return true;
		}

		private static string GetPathFromInputData(IGH_DataAccess DA, GDHComponent component, int paramIndex)
		{
			int pathIndex = 0;
			if (component != null)
			{
				IGH_Structure volatileData = ((GH_Component)component).Params.Input[paramIndex].VolatileData;
				if (((volatileData != null) ? new int?(volatileData.PathCount) : null) > 1)
				{
					pathIndex = DA.Iteration;
				}
			}
			if (component == null)
			{
				return null;
			}
			IGH_Structure volatileData2 = ((GH_Component)component).Params.Input[paramIndex].VolatileData;
			if (volatileData2 == null)
			{
				return null;
			}
			return ((object)volatileData2.Paths[pathIndex]).ToString();
		}

		private static void CollectDataHelper<T>(IGH_DataAccess DA, GDHComponent component, string inputName, InputParamSchema schema, GH_ParamAccess access, ref int inputCount, Resthopper.IO.DataTree<ResthopperObject> dataTree, ref List<string> warnings, ref List<string> errors, bool convertToGeometryBase = false)
		{
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cf: Invalid comparison between Unknown and I4
			//IL_0182: Unknown result type (might be due to invalid IL or missing references)
			//IL_0185: Invalid comparison between Unknown and I4
			string path = "{0}";
			int? paramIndex = ((component != null) ? new int?(((GH_Component)component).Params.IndexOfInputParam(inputName)) : null);
			if (paramIndex > -1)
			{
				path = GetPathFromInputData(DA, component, paramIndex.Value);
			}
			if ((int)access == 0)
			{
				T t = default(T);
				if (DA.GetData<T>(inputName, ref t))
				{
					inputCount = 1;
					if (convertToGeometryBase)
					{
						GeometryBase gb2 = GH_Convert.ToGeometryBase((object)t);
						dataTree.Append(new ResthopperObject(gb2), path);
					}
					else if ((!(t is double) && !(t is int)) || CheckMinMax(t, inputName, schema, ref errors))
					{
						dataTree.Append(new ResthopperObject(t), path);
					}
				}
			}
			else
			{
				if ((int)access == 1)
				{
					List<T> list = new List<T>();
					if (!DA.GetDataList<T>(inputName, list))
					{
						return;
					}
					inputCount = list.Count;
					{
						foreach (T item in list)
						{
							if (convertToGeometryBase)
							{
								GeometryBase gb = GH_Convert.ToGeometryBase((object)item);
								dataTree.Append(new ResthopperObject(gb), path);
								continue;
							}
							if ((item is double || item is int) && !CheckMinMax(item, inputName, schema, ref errors))
							{
								break;
							}
							dataTree.Append(new ResthopperObject(item), path);
						}
						return;
					}
				}
				if ((int)access == 2)
				{
					Type type = typeof(T);
					throw new Exception($"Tree not currently supported for type: {type}");
				}
			}
		}

		private static void CollectDataHelper2<T, GHT>(IGH_DataAccess DA, GDHComponent component, string inputName, InputParamSchema schema, GH_ParamAccess access, ref int inputCount, Resthopper.IO.DataTree<ResthopperObject> dataTree, ref List<string> warnings, ref List<string> errors) where GHT : GH_Goo<T>
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Invalid comparison between Unknown and I4
			//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
			if ((int)access == 2)
			{
				string path = "{0}";
				GH_Structure<GHT> tree = new GH_Structure<GHT>();
				if (!DA.GetDataTree<GHT>(inputName, out tree))
				{
					return;
				}
				{
					foreach (GH_Path treePath in tree.Paths)
					{
						path = ((object)treePath).ToString();
						foreach (GHT item in tree[treePath])
						{
							if ((item is double || item is int) && !CheckMinMax(((GH_Goo<T>)item).Value, inputName, schema, ref errors))
							{
								return;
							}
							dataTree.Append(new ResthopperObject(((GH_Goo<T>)item).Value), path);
						}
					}
					return;
				}
			}
			CollectDataHelper<T>(DA, component, inputName, schema, access, ref inputCount, dataTree, ref warnings, ref errors);
		}

		private static void CollectDataHelperPoints<T>(IGH_DataAccess DA, GDHComponent component, string inputName, InputParamSchema schema, GH_ParamAccess access, ref int inputCount, Resthopper.IO.DataTree<ResthopperObject> dataTree, ref List<string> warnings, ref List<string> errors)
		{
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Expected I4, but got Unknown
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_013b: Unknown result type (might be due to invalid IL or missing references)
			string path = "{0}";
			int? paramIndex = ((component != null) ? new int?(((GH_Component)component).Params.IndexOfInputParam(inputName)) : null);
			if (paramIndex > -1)
			{
				path = GetPathFromInputData(DA, component, paramIndex.Value);
			}
			switch ((int)access)
			{
				case 0:
					{
						GH_Point t = null;
						if (DA.GetData<GH_Point>(inputName, ref t))
						{
							inputCount = 1;
							dataTree.Append(new ResthopperObject(t.Value), path);
						}
						break;
					}
				case 1:
					{
						List<GH_Point> list = new List<GH_Point>();
						if (DA.GetDataList<GH_Point>(inputName, list))
						{
							inputCount = list.Count;
							for (int i = 0; i < list.Count; i++)
							{
								dataTree.Append(new ResthopperObject(list[i].Value), path);
							}
						}
						break;
					}
				case 2:
					{
						GH_Structure<GH_Point> tree = new GH_Structure<GH_Point>();
						if (!DA.GetDataTree<GH_Point>(inputName, out tree))
						{
							break;
						}
						{
							foreach (GH_Path treePath in tree.Paths)
							{
								path = ((object)treePath).ToString();
								foreach (GH_Point item in tree[treePath])
								{
									dataTree.Append(new ResthopperObject(item.Value), path);
								}
							}
							break;
						}
					}
			}
		}

		private static void CollectDataHelperGeometryBase<T>(IGH_DataAccess DA, GDHComponent component, string inputName, InputParamSchema schema, GH_ParamAccess access, ref int inputCount, Resthopper.IO.DataTree<ResthopperObject> dataTree, ref List<string> warnings, ref List<string> errors)
		{
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Expected I4, but got Unknown
			string path = "{0}";
			int? paramIndex = ((component != null) ? new int?(((GH_Component)component).Params.IndexOfInputParam(inputName)) : null);
			if (paramIndex > -1)
			{
				path = GetPathFromInputData(DA, component, paramIndex.Value);
			}
			switch ((int)access)
			{
				case 0:
					{
						IGH_GeometricGoo t = null;
						if (DA.GetData<IGH_GeometricGoo>(inputName, ref t))
						{
							inputCount = 1;
							GeometryBase gb = GH_Convert.ToGeometryBase((object)t);
							dataTree.Append(new ResthopperObject(gb), path);
						}
						break;
					}
				case 1:
					{
						List<IGH_GeometricGoo> list = new List<IGH_GeometricGoo>();
						if (DA.GetDataList<IGH_GeometricGoo>(inputName, list))
						{
							inputCount = list.Count;
							for (int i = 0; i < list.Count; i++)
							{
								GeometryBase gb2 = GH_Convert.ToGeometryBase((object)list[i]);
								dataTree.Append(new ResthopperObject(gb2), path);
							}
						}
						break;
					}
				case 2:
					{
						GH_Structure<IGH_GeometricGoo> tree = new GH_Structure<IGH_GeometricGoo>();
						if (!DA.GetDataTree<IGH_GeometricGoo>(inputName, out tree))
						{
							break;
						}
						{
							foreach (GH_Path treePath in tree.Paths)
							{
								path = ((object)treePath).ToString();
								foreach (IGH_GeometricGoo item in tree[treePath])
								{
									GeometryBase gb3 = GH_Convert.ToGeometryBase((object)item);
									dataTree.Append(new ResthopperObject(gb3), path);
								}
							}
							break;
						}
					}
			}
		}

		internal static GH_ParamAccess AccessFromInput(InputParamSchema input)
		{
			if (!input.TreeAccess)
			{
				if (input.AtLeast == 1 && input.AtMost == 1)
				{
					return (GH_ParamAccess)0;
				}
				if (input.AtLeast == -1 && input.AtMost == -1)
				{
					return (GH_ParamAccess)2;
				}
				return (GH_ParamAccess)1;
			}
			return (GH_ParamAccess)2;
		}

		public Schema CreateSolveInput(IGH_DataAccess DA, bool cacheSolveOnServer, int recursionLevel, out List<string> warnings, out List<string> errors)
		{
			//IL_0095: Unknown result type (might be due to invalid IL or missing references)
			//IL_009a: Unknown result type (might be due to invalid IL or missing references)
			//IL_027c: Unknown result type (might be due to invalid IL or missing references)
			//IL_029b: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0317: Unknown result type (might be due to invalid IL or missing references)
			//IL_0336: Unknown result type (might be due to invalid IL or missing references)
			//IL_0355: Unknown result type (might be due to invalid IL or missing references)
			//IL_0374: Unknown result type (might be due to invalid IL or missing references)
			//IL_0393: Unknown result type (might be due to invalid IL or missing references)
			//IL_03b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_03dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0407: Unknown result type (might be due to invalid IL or missing references)
			//IL_0426: Unknown result type (might be due to invalid IL or missing references)
			//IL_0445: Unknown result type (might be due to invalid IL or missing references)
			//IL_0464: Unknown result type (might be due to invalid IL or missing references)
			//IL_048e: Unknown result type (might be due to invalid IL or missing references)
			//IL_04ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_04cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_04eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_050a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0529: Unknown result type (might be due to invalid IL or missing references)
			//IL_0548: Unknown result type (might be due to invalid IL or missing references)
			//IL_0567: Unknown result type (might be due to invalid IL or missing references)
			//IL_0586: Unknown result type (might be due to invalid IL or missing references)
			//IL_05a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_05c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_05e3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0602: Unknown result type (might be due to invalid IL or missing references)
			//IL_061f: Unknown result type (might be due to invalid IL or missing references)
			//IL_063b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0657: Unknown result type (might be due to invalid IL or missing references)
			//IL_0673: Unknown result type (might be due to invalid IL or missing references)
			//IL_0682: Unknown result type (might be due to invalid IL or missing references)
			//IL_0685: Invalid comparison between Unknown and I4
			warnings = new List<string>();
			errors = new List<string>();
			Schema schema = new Schema();
			schema.RecursionLevel = recursionLevel;
			schema.AbsoluteTolerance = GetDocumentTolerance();
			schema.AngleTolerance = GetDocumentAngleTolerance();
			schema.ModelUnits = GetDocumentUnits();
			schema.CacheSolve = cacheSolveOnServer;
			Dictionary<string, Tuple<InputParamSchema, IGH_Param>> inputs = GetInputParams();
			if (inputs != null)
			{
				foreach (KeyValuePair<string, Tuple<InputParamSchema, IGH_Param>> kv in inputs)
				{
					kv.Value.Deconstruct(out var item, out var item2);
					InputParamSchema input = item;
					IGH_Param param = item2;
					string inputName = kv.Key;
					string computeName = input.Name;
					GH_ParamAccess access = AccessFromInput(input);
                    Resthopper.IO.DataTree<ResthopperObject> dataTree = new Resthopper.IO.DataTree<ResthopperObject>();
					dataTree.ParamName = computeName;
					schema.Values.Add(dataTree);
					int inputListCount = 0;
					if (!(param is Param_Arc))
					{
						if (!(param is Param_Boolean))
						{
							if (!(param is Param_Box))
							{
								if (!(param is Param_Brep))
								{
									if (!(param is Param_Circle))
									{
										if (!(param is Param_Colour))
										{
											if (!(param is Param_Complex))
											{
												if (!(param is Param_Culture))
												{
													if (!(param is Param_Curve))
													{
														if (!(param is Param_Field))
														{
															if (!(param is Param_FilePath))
															{
																if (param is Param_GenericObject)
																{
																	throw new Exception("generic param not supported");
																}
																if (!(param is Param_Geometry))
																{
																	if (param is Param_Group)
																	{
																		throw new Exception("group param not supported");
																	}
																	if (!(param is Param_Guid))
																	{
																		if (!(param is Param_Integer))
																		{
																			if (!(param is Param_Interval))
																			{
																				if (!(param is Param_Interval2D))
																				{
																					if (param is Param_LatLonLocation)
																					{
																						throw new Exception("latlonlocation param not supported");
																					}
																					if (!(param is Param_Line))
																					{
																						if (!(param is Param_Matrix))
																						{
																							if (!(param is Param_Mesh))
																							{
																								if (!(param is Param_MeshFace))
																								{
																									if (!(param is Param_MeshParameters))
																									{
																										if (!(param is Param_Number))
																										{
																											if (!(param is Param_Plane))
																											{
																												if (!(param is Param_Point))
																												{
																													if (!(param is Param_Rectangle))
																													{
																														if (!(param is Param_String))
																														{
																															if (!(param is Param_StructurePath))
																															{
																																if (!(param is Param_SubD))
																																{
																																	if (!(param is Param_Surface))
																																	{
																																		if (!(param is Param_Time))
																																		{
																																			if (!(param is Param_Transform))
																																			{
																																				if (!(param is Param_Vector))
																																				{
																																					if (param is GH_NumberSlider)
																																					{
																																						RemoteDefinition.CollectDataHelper2<double, GH_Number>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																																					}
																																				}
																																				else
																																				{
																																					RemoteDefinition.CollectDataHelper2<Vector3d, GH_Vector>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																																				}
																																			}
																																			else
																																			{
																																				RemoteDefinition.CollectDataHelper2<Transform, GH_Transform>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																																			}
																																		}
																																		else
																																		{
																																			RemoteDefinition.CollectDataHelper2<DateTime, GH_Time>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																																		}
																																	}
																																	else
																																	{
																																		CollectDataHelper<Surface>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																																	}
																																}
																																else
																																{
																																	RemoteDefinition.CollectDataHelper2<SubD, GH_SubD>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																																}
																															}
																															else
																															{
																																RemoteDefinition.CollectDataHelper2<GH_Path, GH_StructurePath>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																															}
																														}
																														else
																														{
																															RemoteDefinition.CollectDataHelper2<string, GH_String>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																														}
																													}
																													else
																													{
																														RemoteDefinition.CollectDataHelper2<Rectangle3d, GH_Rectangle>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																													}
																												}
																												else
																												{
																													CollectDataHelperPoints<Point3d>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																												}
																											}
																											else
																											{
																												RemoteDefinition.CollectDataHelper2<Plane, GH_Plane>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																											}
																										}
																										else
																										{
																											RemoteDefinition.CollectDataHelper2<double, GH_Number>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																										}
																									}
																									else
																									{
																										RemoteDefinition.CollectDataHelper2<MeshingParameters, GH_MeshingParameters>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																									}
																								}
																								else
																								{
																									RemoteDefinition.CollectDataHelper2<MeshFace, GH_MeshFace>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																								}
																							}
																							else
																							{
																								RemoteDefinition.CollectDataHelper2<Mesh, GH_Mesh>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																							}
																						}
																						else
																						{
																							RemoteDefinition.CollectDataHelper2<Matrix, GH_Matrix>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																						}
																					}
																					else
																					{
																						RemoteDefinition.CollectDataHelper2<Line, GH_Line>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																					}
																				}
																				else
																				{
																					RemoteDefinition.CollectDataHelper2<UVInterval, GH_Interval2D>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																				}
																			}
																			else
																			{
																				RemoteDefinition.CollectDataHelper2<Interval, GH_Interval>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																			}
																		}
																		else
																		{
																			RemoteDefinition.CollectDataHelper2<int, GH_Integer>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																		}
																	}
																	else
																	{
																		RemoteDefinition.CollectDataHelper2<Guid, GH_Guid>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																	}
																}
																else
																{
																	CollectDataHelperGeometryBase<IGH_GeometricGoo>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
																}
															}
															else
															{
																RemoteDefinition.CollectDataHelper2<string, GH_String>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
															}
														}
														else
														{
															CollectDataHelper<GH_Field>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
														}
													}
													else
													{
														RemoteDefinition.CollectDataHelper2<Curve, GH_Curve>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
													}
												}
												else
												{
													RemoteDefinition.CollectDataHelper2<CultureInfo, GH_Culture>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
												}
											}
											else
											{
												RemoteDefinition.CollectDataHelper2<Complex, GH_ComplexNumber>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
											}
										}
										else
										{
											RemoteDefinition.CollectDataHelper2<Color, GH_Colour>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
										}
									}
									else
									{
										RemoteDefinition.CollectDataHelper2<Circle, GH_Circle>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
									}
								}
								else
								{
									RemoteDefinition.CollectDataHelper2<Brep, GH_Brep>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
								}
							}
							else
							{
								RemoteDefinition.CollectDataHelper2<Box, GH_Box>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
							}
						}
						else
						{
							RemoteDefinition.CollectDataHelper2<bool, GH_Boolean>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
						}
					}
					else
					{
						RemoteDefinition.CollectDataHelper2<Arc, GH_Arc>(DA, _parentComponent, inputName, input, access, ref inputListCount, dataTree, ref warnings, ref errors);
					}
					if ((int)access == 1)
					{
						if (inputListCount < input.AtLeast)
						{
							warnings.Add($"{input.Name} requires at least {input.AtLeast} items");
						}
						if (inputListCount > input.AtMost)
						{
							warnings.Add($"{input.Name} requires at most {input.AtMost} items");
						}
					}
				}
			}
			schema.Pointer = Path;
			if (GetPathType() == PathType.Server)
			{
				string pointer = new Uri(Path).AbsolutePath;
				schema.Pointer = pointer.Substring(1);
			}
			_parentComponent.HTTPRecord.Schema = schema;
			return schema;
		}
	}
}