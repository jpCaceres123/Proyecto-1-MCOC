using System;
using UnityEngine;

namespace StructuralLab {
    [Serializable] public class ProjectData {
        public int schemaVersion; public string generated, units, title, sourceWorkbook, pythonExecutable, projectRoot;
        public float gap; public ModelData model; public LoadCase[] cases; public Capacity[] capacities;
        public LoadRow[] loadRows; public Validation validation; public string[] notes;
        public Overrides config;
    }
    [Serializable] public class ModelData {
        public NodeData[] nodes; public ElementData[] elements; public WallData[] walls;
        public Diaphragm[] diaphragms; public Slab[] slabs; public Tributary[] tributaries; public float[] levels;
    }
    [Serializable] public class NodeData {
        public int id; public double x,y,z; public string building,kind; public bool fix;
        public Vector3 World {get {return new Vector3((float)x,(float)z,(float)y);}}
    }
    [Serializable] public class ElementData {
        public int id,source,wall,i,j; public string kind,building;
        public float b,h,L,A,Iy,Iz,J,stiffnessScale; public float[] ex,ey,ez;
    }
    [Serializable] public class WallData {public int id; public float ax,ay,bx,by,z0,z1,thickness;public string building;}
    [Serializable] public class Diaphragm {public int id;public string building;public float x,y,z,weight,force,mass_t;public int[] nodes;}
    [Serializable] public class Point3 {public float x,y,z; public Vector3 World {get{return new Vector3(x,z,y);}}}
    [Serializable] public class Point2 {public float x,y;}
    [Serializable] public class Slab {public int id;public float z,area;public string state;public Point3[] points;}
    [Serializable] public class Tributary {public int slab;public string edge;public Point2[] points;}
    [Serializable] public class LoadCase {
        public string name;public NodeResult[] nodes;public ElementResult[] elements;
        public double[] applied,reactions;public double forceResidual,momentResidual,maxDisplacement_m;
    }
    [Serializable] public class NodeResult {public int id;public double[] u,reaction;}
    [Serializable] public class ElementResult {public int id;public double[] forces;public Diagram[] diagram;}
    [Serializable] public class Diagram {public double t,N,Vy,Vz,T,My,Mz;}
    [Serializable] public class Capacity {
        public string name,kind,assumption,stopReason;public int elementId;public float b,h,steelArea;
        public PM[] pm,whitney;public MPhi[] mphi;public CompressionCheck checkCompression;
    }
    [Serializable] public class PM {public double P,M;}
    [Serializable] public class MPhi {public double curvature,moment,axial;}
    [Serializable] public class CompressionCheck {public double opensees,independent,relative;}
    [Serializable] public class LoadRow {
        public int slab_id,beam_id;public string zone,edge,distribution;
        public double level_z_m,tributary_area_m2,q_G_kN_m2,q_SC_kN_m2,dead_load_kN,live_load_kN;
    }
    [Serializable] public class ErrorMetric {public double maxAbs,relative;}
    [Serializable] public class Superposition {public ErrorMetric displacements,forces;}
    [Serializable] public class Validation {public bool checksPassed,academicAssumptions;public Superposition superposition;public double diaphragmError;}
    [Serializable] public class Overrides {
        public double stiffness_multiplier=1,beam_section_multiplier=1,column_section_multiplier=1,wall_stiffness_multiplier=1;
        public double seismic_coefficient=.1,G_scale=1,Q_scale=1;
    }
    public class PickElement : MonoBehaviour {public int id;}
    public class PickNode : MonoBehaviour {public int id;}
}
