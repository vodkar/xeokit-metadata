using System;
using System.Collections.Generic;
using System.IO;
using CommandLine;
using System.Linq;

namespace XeokitMetadata {
    /// <summary>
    /// This command line application extracts the hierarchical structure of
    /// the building elements and creates a JSON output compatible with the
    /// xeokit-sdk's metadata model.
    /// See: https://github.com/xeokit/xeokit-sdk/wiki/Viewing-BIM-Models-Offline
    ///
    /// The program takes two arguments, first is the path to the IFC file, the
    /// second one is the path to the output JSON. Currently only IFC 2x3 is
    /// supported.
    ///
    /// Xbim's SDK is used to process the IFC file.
    /// 
    /// The JSON schema can be found in the sources:
    /// https://github.com/bimspot/xeokit-metadata-utils
    /// </summary>
    internal static class Program {
        public class Options {
            [Value(0, MetaName ="IFC file", Required = true, HelpText = "Path to IFC file")]
            public string ifcPath { get; set; }

            [Value(1, MetaName = "JSON target file", Required = true, HelpText = "Path to json target file")]
            public string jsonPath { get; set; }

            [Option('p', "with-props", HelpText = "Properties to extract from ifc objects", Default = null)]
            public IEnumerable<string>? properties { get; set; }
        }

        private static void Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<Options>(args);

            result.WithParsed<Options>(o =>
            {
                var ifcPath = o.ifcPath;

                if (File.Exists(ifcPath) == false)
                {
                    Console
                      .WriteLine("The IFC file does not exists at path: {0}", ifcPath);
                    Environment.Exit(1);
                }

                // TODO: create path?
                var jsonPath = o.jsonPath;

                try
                {
                    var props = o.properties is null ? null : o.properties.ToList();
                    var metaModel = MetaModel.fromIfc(ifcPath, props);
                    metaModel.toJson(jsonPath);
                    Environment.Exit(0);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    Environment.Exit(1);
                    }
            }).WithNotParsed<Options>(errors =>
            {
                Console.WriteLine("Please specify the path to the IFC and the output json.");
                Console.WriteLine(@"
Usage:
          
$ xeokit-metadata /path/to/some.ifc /path/to/output.json

"
               );
            }
            );

        }
    }
}