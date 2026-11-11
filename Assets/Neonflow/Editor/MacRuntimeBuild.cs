using System.IO;
using UnityEditor;
using UnityEditor.Build;

namespace Neonflow.Editor
{
    public sealed class MacRuntimeBuild : BuildPlayerProcessor
    {
        public override void PrepareForBuild(BuildPlayerContext context)
        {
            if(context.BuildPlayerOptions.target != BuildTarget.StandaloneOSX) return;
            foreach(string architecture in new[]{"arm64","x86_64"})
            {
                string folder=Path.GetFullPath("Tools/Recognition/mac-runtime/"+architecture);
                if(!File.Exists(Path.Combine(folder,"python/bin/python3.11")))
                    throw new BuildFailedException("Prepare native Mac runtime first: Tools/Recognition/prepare_mac_runtime.py");
                context.AddAdditionalPathToStreamingAssets(folder,"Recognition/mac-runtime/"+architecture);
            }
        }
    }
}
