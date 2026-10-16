var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
switch (args.Length > 1 ? args[1] : "")
{
    case "bottleneck": BottleneckStudy.Run(root); break;
    case "causal": CausalStudy.Run(root); break;
    case "access-focus": AccessStudy.Run(root); break;
    case "access-focus-improved": AccessStudy.Run(root, true); break;
    default: throw new ArgumentException("Modes: causal, bottleneck, access-focus, access-focus-improved");
}
