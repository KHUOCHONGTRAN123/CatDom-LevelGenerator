using CatDom.CoreSolver;
using CatDom.CoreSolver.IO;
using Newtonsoft.Json;
using System.Security.Cryptography;
using Ref=CatDom.Reference.V42;

internal static class BottleneckStudy
{
 internal static void Run(string root)
 {
  var json=File.ReadAllText(Path.Combine(root,"Levels/Level00115.json"));
  var catalogJson=File.ReadAllText(Path.Combine(root,"IndependentSolver/IO/HoleShapes.json"));
  var input=SolverJson.ReadLevel(json,SolverJson.ReadCatalog(catalogJson));
  var output=new List<object>();
  foreach(bool improved in new[]{false,true})
  {
   using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(20));
   var r=improved?CatLevelSolver.SolveLevelWithProgress(json,cancel.Token,null,0):CatLevelSolver.SolveLevel(json,cancel.Token);
   if(r.status!=SolverStatus.Solved)throw new Exception(r.status.ToString());
   var reference=Ref.CatLevelSolver.VerifyPlan(Ref.IO.SolverJson.ReadLevel(json,Ref.IO.SolverJson.ReadCatalog(catalogJson)),JsonConvert.DeserializeObject<List<Ref.SolverMove>>(JsonConvert.SerializeObject(r.moves)),10,true);
   if(reference.status!=Ref.SolveStatus.Solved || reference.finalState.remainingCatIds.Count!=0 || reference.finalState.holes.Where(h=>h.color<1000).Any(h=>!h.finished))throw new Exception("Independent replay failed");
   var prefix=new List<EngineMove>();var states=new List<object>();
   var previous=CatLevelSolver.VerifyPlan(input,prefix).finalState;
   for(int i=0;i<r.moves.Count;i++)
   {
    var m=r.moves[i];prefix.Add(new(){holeId=m.holeId,start=m.start,path=m.path,eaten=m.eaten});
    var s=CatLevelSolver.VerifyPlan(input,prefix).finalState;
    var finished=s.holes.Where(h=>h.finished&&!previous.holes[h.id].finished).Select(h=>h.id).ToArray();
    states.Add(new{drag=i+1,hole=m.holeId,start=m.start,end=m.path[^1],eaten=m.eaten,finished,snapshot=s});
    if(m.eaten.Count>0||i==0)Console.WriteLine($"{(improved?"I":"H")} {i+1} h{m.holeId} {JsonConvert.SerializeObject(m.eaten)} clear={string.Join(",",finished)} positions="+string.Join(" ",s.holes.Where(h=>!h.finished).Select(h=>$"{h.id}:{h.position.x},{h.position.y}")));
    previous=s;
   }
   output.Add(new{mode=improved?"improved":"hybrid",independentReplay=reference.status.ToString(),states});
  }
  string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
  File.WriteAllText(Path.Combine(root,"Generate Docs/research/bottleneck-original-00115.json"),JsonConvert.SerializeObject(new{level="Level00115",inputHash=Hash(System.Text.Encoding.UTF8.GetBytes(json)),catalogHash=Hash(System.Text.Encoding.UTF8.GetBytes(catalogJson)),solverHash=Hash(File.ReadAllBytes(typeof(CatLevelSolver).Assembly.Location)),referenceHash=Hash(File.ReadAllBytes(typeof(Ref.CatLevelSolver).Assembly.Location)),runs=output},Formatting.Indented));
 }
}
