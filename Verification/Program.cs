using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Product = CatDom.CoreSolver;
using Reference = CatDom.Reference.V42;
using ReferenceJson = CatDom.Reference.V42.IO.SolverJson;

string root = Path.GetFullPath(args.Length > 1 ? args[1] : ".");
if(args.Length>0&&args[0]=="spawner-result-replay"){SpawnerResultReplay.Run(root,args[2],args.Length>3?args[3]:null);return;}
if(args.Length>0&&args[0]=="spawner-tests"){SpawnerTests.Run();V3IndependentSpawnerTests.Run(root);return;}
if(args.Length>0&&args[0]=="bomb-priority-tests"){BombPriorityTests.Run();V3BombTests.Run(root);BombQueueRelaxationTests.Run();V3IndependentSpawnerTests.Run(root);return;}
if(args.Length>0&&args[0]=="expand-trace"){ExpandTrace.Run(root,args[2],args[3],int.Parse(args[4]),args.Length>5?args[5]:null);return;}
if(args.Length>0&&args[0]=="expand-tests"){ExpandTests.Run();PreparedPhaseCacheTests.Run();PhaseStateKeyTests.Run();V3IndependentCoverTests.Run(root);return;}
if(args.Length>0&&args[0]=="group3-barrie-tests"){Group3BarrieTests.Run(root);return;}
if(args.Length>0&&args[0]=="group3-elevator-geometry-tests"){Group3ElevatorGeometryTests.Run(root);return;}
if(args.Length>0&&args[0]=="group2-profile"){Group2Profile.Run(root,args[2],args[3]);return;}
if(args.Length>0&&args[0]=="group1-stage-trace"){Group1StageTrace.Run(root,args[2]);return;}
if(args.Length>0&&args[0]=="screen-corpus"){V3Screen.Run(root,null,args[2],args[3]);return;}
if(args.Length>0&&args[0]=="v3-replay-existing"){V3ReplayExisting.Run(root,args.Length>2?args[2]:null,args.Length>3?args[3]:null,args.Length>4?args[4]:null);return;}
if(args.Length>0&&args[0]=="v3-bomb-tests"){V3BombTests.Run(root);return;}
if(args.Length>0&&args[0]=="v3-countdown-oracle-tests"){V3CountdownOracleTests.Run();return;}
if(args.Length>0&&args[0]=="v3-secret-tests"){V3SecretTests.Run(root);return;}
if(args.Length>0&&args[0]=="v3-barricade-tests"){V3BarricadeTests.Run(root);return;}
if(args.Length>0&&args[0]=="v3-mystery-tests"){V3MysteryTests.Run(root);return;}
if(args.Length>0&&args[0]=="v3-mechanics-tests"){V3MechanicsTests.Run(root);return;}
if(args.Length>0&&args[0]=="v3-fence-tests"){V3FenceTests.Run(root);return;}
if(args.Length>0&&args[0]=="v3-curtain-tests"){V3CurtainTests.Run(root);return;}
if(args.Length>0&&args[0]=="v3-screen"){V3Screen.Run(root,args.Length>2?args[2]:null,args.Length>3?args[3]:null,args.Length>4?args[4]:null);return;}
if(args.Length>0&&args[0]=="generator-oracle"){GeneratorOracle.Run(root);return;}
if(args.Length>0&&args[0]=="route-graph-tests"){RouteGraphTests.Run(root);return;}
var random=new Random(42);
HybridTests.Run(root);
CheckpointTests.Run(root);
CoverMaskCacheTests.Run();
PreparedPhaseCacheTests.Run();
LinkedAssignmentTests.Run();
PhaseStateKeyTests.Run();
IdleConsumptionTests.Run();
BombQueueRelaxationTests.Run();
Group2CompatibilityTests.Run(root);
PlugTests.Run();
SpawnerTests.Run();
ExpandTests.Run();
V3IndependentReplayTests.Run(root);
RouteTests.Run();
var heap=new Product.StableMinHeap<int>();
var orderedHeap=new SortedSet<(long score,int serial,int node)>(Comparer<(long score,int serial,int node)>.Create((a,b)=>a.score!=b.score?a.score.CompareTo(b.score):a.serial.CompareTo(b.serial)));
for(int i=0;i<10000;i++){var entry=((long)random.Next(0,100),i,i);heap.Add(entry);orderedHeap.Add(entry);}
while(heap.Count>0){var expected=orderedHeap.Min;orderedHeap.Remove(expected);Assert(heap.Pop()==expected,"Heap tie-break mismatch");}
for(int i=0;i<10000;i++)
{
    var a=new BigInteger(random.NextInt64())<<random.Next(0,210);var b=new BigInteger(random.NextInt64())<<random.Next(0,210);
    var x=new Product.CellMask(a);var y=new Product.CellMask(b);
    Assert((x&y).ToBigInteger()==(a&b)&&(x|y).ToBigInteger()==(a|b)&&(x^y).ToBigInteger()==(a^b),"Bitboard boolean mismatch");
    Assert((~x).ToBigInteger()==~a&&((~x)&y).ToBigInteger()==((~a)&b),"Bitboard complement mismatch");
    Assert((x<<i%180).ToBigInteger()==(a<<i%180)&&(x-y).ToBigInteger()==a-b,"Bitboard arithmetic mismatch");
    int count=0;var bits=a;while(!bits.IsZero){bits&=bits-1;count++;}Assert(x.PopCount()==count,"PopCount mismatch");
}
var invalid=Product.CatLevelSolver.SolveLevel("{");Assert(invalid.status==Product.SolverStatus.InvalidInput&&double.IsFinite(invalid.solveTimeMs),"Invalid JSON result");
foreach(var malformed in new[]{"{\"sizeX\":3,\"sizeY\":3,\"holeInfos\":[5]}","{\"sizeX\":\"abc\",\"sizeY\":3}","null","[]",""})
{var result=Product.CatLevelSolver.SolveLevel(malformed);Assert(result.status==Product.SolverStatus.InvalidInput&&double.IsFinite(result.solveTimeMs)&&result.solveTimeMs>=0,"Malformed level must return InvalidInput");}
foreach(int width in new[]{64,65,128,129,256})
{
    var board=new Reference.BoardInput {width=width,height=1};
    board.holes.Add(new Reference.HoleInput {id=0,color=1,remaining=1,position=new Reference.Cell(0,0),footprint=new[]{new Reference.Cell(0,0)}});
    board.cats.Add(new Reference.CatInput {id=0,color=1,position=new Reference.Cell(width-1,0)});
    var result=V1TestAdapter.Solve(board,Reference.SolverConfig.CreateRouteClearingDefault());
    Assert(result.status==Reference.SolveStatus.Solved&&V1TestAdapter.VerifyPlan(board,result.moves,0,true).status==Reference.SolveStatus.Solved,"Mask/packed-state boundary failed "+width);
}
using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();var result=Product.CatLevelSolver.SolveLevel("{}",cancelled.Token);Assert(result.status==Product.SolverStatus.Cancelled&&result.solveTimeMs>=0,"Cancellation result");}
var fixture=File.ReadAllText(Path.Combine(root,"Levels/Level00001.json"));
var unsupported=JObject.Parse(fixture);unsupported["unknownMechanic"]=true;
var unsupportedResult=Product.CatLevelSolver.SolveLevel(unsupported.ToString());
Assert(unsupportedResult.status==Product.SolverStatus.UnsupportedMechanics&&double.IsFinite(unsupportedResult.solveTimeMs)&&unsupportedResult.solveTimeMs>=0,"Unsupported mechanics timing");
var noNext=Product.CatLevelSolver.SolveLevel("{\"sizeX\":1,\"sizeY\":1,\"catInfos\":[{\"colorId\":1,\"position\":{\"x\":0,\"y\":0}}]}");
Assert(noNext.status==Product.SolverStatus.NoNextCatReachable&&double.IsFinite(noNext.solveTimeMs)&&noNext.solveTimeMs>=0,"No-next-cat timing");
using(var runningCancellation=new CancellationTokenSource())
{
    runningCancellation.CancelAfter(30);
    var cancelledSearch=Product.CatLevelSolver.SolveLevel(File.ReadAllText(Path.Combine(root,"Levels/Level00206.json")),runningCancellation.Token);
    Assert(cancelledSearch.status==Product.SolverStatus.Cancelled&&double.IsFinite(cancelledSearch.solveTimeMs)&&cancelledSearch.solveTimeMs>=0,"Cancellation during search");
}
var parallel=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>Task.Run(()=>Product.CatLevelSolver.SolveLevel(fixture))));
Assert(parallel.All(r=>r.status==Product.SolverStatus.Solved),"Concurrent solves failed");
Console.WriteLine("PASS mask equivalence, malformed JSON, cancellation, concurrent calls");
static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}










