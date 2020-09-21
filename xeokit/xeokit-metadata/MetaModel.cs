using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Xbim.Ifc;
using Xbim.Ifc2x3.Kernel;
using Xbim.Ifc2x3.PropertyResource;
using Xbim.Ifc4.Interfaces;

namespace XeokitMetadata {
    /// <summary>
    ///   The MetaObject is used to serialise the building elements within the IFC
    ///   model. It is a representation of a single element (e.g. IfcProject,
    ///   IfcStorey, IfcWindow, etc.).
    /// </summary>
    public struct MetaObject {
        /// <summary>
        ///   The GlobalId of the building element
        /// </summary>
        public string id;

        /// <summary>
        ///   The Name of the building element
        /// </summary>
        public string name;

        /// <summary>
        ///   The IFC type of the building element, e.g. 'IfcStandardWallCase'
        /// </summary>
        public string type;

        /// <summary>
        ///   The GlobalId of the parent element if any.
        /// </summary>
        public string parent;

#nullable enable
        /// <summary>
        ///   The properties of the building element.
        /// </summary>
        public Dictionary<string, object>? properties;
#nullable disable
    }

    /// <summary>
    ///   The MetaModel is used to serialise the building elements within the IFC
    ///   model. It is the representation of the complete JSON version of the
    ///   structure.
    /// </summary>
    public struct MetaModel {
        /// <summary>
        ///   Initializes `MetaModel` instance.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="projectId"></param>
        /// <param name="author"></param>
        /// <param name="createdAt"></param>
        /// <param name="schema"></param>
        /// <param name="creatingApplication"></param>
        public void init(
          string id,
          string projectId,
          string author,
          string createdAt,
          string schema,
          string creatingApplication) {
            this.id = id;
            this.projectId = projectId;
            this.author = author;
            this.createdAt = createdAt;
            this.schema = schema;
            this.creatingApplication = creatingApplication;
        }

        /// <summary>
        ///   The Id field is populated with the name of the project.
        /// </summary>
        public string id;

        /// <summary>
        ///   The GlobalId of the project.
        /// </summary>
        public string projectId;

        /// <summary>
        ///   The author of the project.
        /// </summary>
        public string author;

        /// <summary>
        ///   The creation date of the project.
        /// </summary>
        public string createdAt;

        /// <summary>
        ///   The schema of the ifc model.
        /// </summary>
        public string schema;

        /// <summary>
        ///   The application with which the model was created.
        /// </summary>
        public string creatingApplication;

        /// <summary>
        ///   A list of all building elements as MetaObjects within the project.
        /// </summary>
        public List<MetaObject> metaObjects;

        /// <summary>
        ///   The convenience initialiser creates and returns an instance of the
        ///   MetaModel by parsing the IFC at the provided path.
        /// </summary>
        /// <param name="ifcPath">A string path of the IFC path.</param>
        /// <returns>Returns the complete MetaModel of the IFC.</returns>
        public static MetaModel fromIfc(string ifcPath, List<string> withProperties) {
            using (var model = IfcStore.Open(ifcPath)) {

                var project = model.Instances.FirstOrDefault<IIfcProject>();

                var header = model.Header;
                var metaModel = new MetaModel();
                metaModel.init(
                  project.Name,
                  project.GlobalId,
                  getAuthor(header.FileName.AuthorName),
                  header.TimeStamp,
                  header.SchemaVersion,
                  header.CreatingApplication);

                var metaObjects = extractHierarchy(project, withProperties: withProperties);
                metaModel.metaObjects = metaObjects;
                return metaModel;
            }
        }
        private static Dictionary<string, object> getObjectProperties(IIfcObjectDefinition objectDefinition, List<string> propertiesNames) {
            IfcObject ifcObject = objectDefinition as IfcObject;
            if (ifcObject == null || propertiesNames == null) {
                return null;
            }
            Dictionary<string, object> tempDict = new Dictionary<string, object>();
            foreach (IfcPropertySet propertySet in ifcObject.PropertySets) {
                foreach (IfcProperty property in propertySet.HasProperties) {
                    IfcPropertySingleValue singleProperty = property as IfcPropertySingleValue;
                    if (singleProperty != null && propertiesNames.Contains(singleProperty.Name)) {
                        tempDict[singleProperty.Name.ToString()] = singleProperty.NominalValue;
                    }
                }
            }
            return tempDict;
        }

        /// <summary>
        ///   Method returns the names of authors in one string,
        ///   separated by ";".
        /// </summary>
        /// <param name="authors">List of authors.</param>
        /// <returns>Authors names.</returns>
        private static string getAuthor(IList<string> authors) {
            var author = "";
            foreach (var item in authors) {
                author += item;
                //separator of authors
                if (!item.Equals(authors.Last()))
                    author += ";";
            }
            return author;
        }

        /// <summary>
        ///   The method selects all IIfcSpatialStructureElement-s recursively and
        ///   creates MetaObject of them.
        /// </summary>
        /// <param name="objectDefinition">
        ///   Accepts an IIfcObjectDefinition parameter, which related elements are
        ///   then iterated in search for additional structure elements.
        /// </param>
        /// <returns>
        ///   Returns a flattened list of all MetaObject-s related to the provided
        ///   IIfcObjectDefinition.
        /// </returns>
        private static List<MetaObject> extractHierarchy(
          IIfcObjectDefinition objectDefinition,
          string parentId = null,
          List<string> withProperties = null
            ) {
            var metaObjects = new List<MetaObject>();

            var parentObject = new MetaObject {
                id = objectDefinition.GlobalId,
                name = objectDefinition.Name,
                type = objectDefinition.GetType().Name,
                parent = parentId,
                properties = getObjectProperties(objectDefinition, withProperties),
            };


            metaObjects.Add(parentObject);

            var spatialElement = objectDefinition as IIfcSpatialStructureElement;

            if (spatialElement != null) {
                var containedElements = spatialElement
                  .ContainsElements
                  .SelectMany(rel => rel.RelatedElements);


                foreach (var element in containedElements) {
                    var mo = new MetaObject {
                        id = element.GlobalId,
                        name = element.Name,
                        type = element.GetType().Name,
                        parent = spatialElement.GlobalId,
                        properties = getObjectProperties(objectDefinition, withProperties),
                    };

                    metaObjects.Add(mo);
                    extractRelatedObjects(
                      element,
                      ref metaObjects,
                      mo.id,
                      withProperties);
                }
            }

            extractRelatedObjects(
              objectDefinition,
              ref metaObjects,
              parentObject.id,
              withProperties);

            return metaObjects;
        }

        /// <summary>
        ///   Method extracts related objects hierarchy,
        /// then add children to metaObjects.
        /// </summary>
        /// <param name="objectDefinition">
        ///  Accepts an IIfcObjectDefinition parameter, which related elements are
        ///  then iterated in search for additional structure elements.
        /// </param>
        /// <param name="metaObjects">Reference of 'MetaObject' list</param>
        /// <param name="parentObjId">Id of parent object.</param>
        private static void extractRelatedObjects(
          IIfcObjectDefinition objectDefinition,
          ref List<MetaObject> metaObjects,
          string parentObjId,
          List<string> withProperties = null) {

            var relatedObjects = objectDefinition
              .IsDecomposedBy
              .SelectMany(r => r.RelatedObjects);

            foreach (var item in relatedObjects) {
                var children = extractHierarchy(item, parentObjId, withProperties);
                metaObjects.AddRange(children);
            }
        }

        /// <summary>
        ///   The async method serialises the MetaModel object to a JSON string,
        ///   then writes it out to the provided path.
        ///
        ///   During the serialisation, all property names are turned into lower
        ///   camel case.
        /// </summary>
        /// <param name="jsonPath">
        ///   The path of the output JSON file.
        /// </param>
        public void toJson(string jsonPath) {
            using (var outputFile = new StreamWriter(jsonPath)) {
                outputFile.Write(serialize());
            }
        }

        /// <summary>
        ///   The method serialises the MetaModel object to a JSON string.
        /// </summary>
        /// <returns>Returns the serialized JSON string.</returns>
        public string serialize() {
            var contractResolver = new DefaultContractResolver {
                NamingStrategy = new CamelCaseNamingStrategy()
            };

            var settings = new JsonSerializerSettings {
                ContractResolver = contractResolver,
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };

            var output = JsonConvert.SerializeObject(this, settings);
            return output;
        }

    }
}