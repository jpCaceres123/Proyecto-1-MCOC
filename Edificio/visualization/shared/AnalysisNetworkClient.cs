using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Canonical source. sync_network_clients.ps1 copies this unchanged into each Unity project.
public sealed class AnalysisNetworkClient : MonoBehaviour
{
    [Serializable] public class Edit { public string kind;public int id;public bool changeLoad,changeSection;public double q,b,h; }
    [Serializable] public class Input { public int contractVersion=1;public string requestId,modelHash,operation="analysis";public Edit[] changes=new Edit[0];public int elementTag,barsPerFace=5;public double diameter_m=.028; }
    [Serializable] public class Job { public string id,status,error,operation; }
    [Serializable] public class FileInfo { public string name,sha256; }
    [Serializable] public class Manifest { public int contractVersion;public string modelHash,revision,resultsHash;public FileInfo[] files; }
    [Serializable] public class Health { public int contractVersion;public string modelHash,status; }
    public string Server="http://127.0.0.1:8765",Token="",Status="Backend no conectado";
    public bool Busy { get; private set; }
    public string CurrentJob { get; private set; }
    public string Revision { get; private set; }
    int generation;
    bool cancel;
    string response,error;
    public static string BaseHash { get { var hash=Resources.Load<TextAsset>("honors_base_hash")??Resources.Load<TextAsset>("semana5_modelo_hash");return hash?hash.text.Trim():""; } }
    public void Cancel(){cancel=true;Status="Solicitando cancelación…";}
    public IEnumerator Submit(Input input,Action<string,Manifest> accept)
    {
        if(Busy){Status="Ya hay una solicitud activa";yield break;}
        Uri uri;IPAddress ip;
        if(!Uri.TryCreate(Server,UriKind.Absolute,out uri)||uri.Scheme!="http"||!IPAddress.TryParse(uri.Host,out ip)||!Private(ip)||Token.Length<24){Status="Usa IP privada http y token de al menos 24 caracteres";yield break;}
        if(input.modelHash!=BaseHash||input.modelHash.Length!=64){Status="Modelo local no identificado; no se envía";yield break;}
        Busy=true;cancel=false;int mine=++generation;input.requestId=Guid.NewGuid().ToString();
        try {
            yield return Http("GET","/health",null);
            if(error!=null){Status=error;yield break;}
            Health health=JsonUtility.FromJson<Health>(response);
            if(health.contractVersion!=1||health.modelHash!=input.modelHash){Status="Modelo/contrato del servidor distinto al modelo base";yield break;}
            yield return Http("POST","/jobs",JsonUtility.ToJson(input));
            if(error!=null){Status=error;yield break;}
            Job job=JsonUtility.FromJson<Job>(response);CurrentJob=job.id;float start=Time.realtimeSinceStartup;
            while(job.status=="pending"||job.status=="running"){
                if(cancel||Time.realtimeSinceStartup-start>630){yield return Http("DELETE","/jobs/"+job.id,null);Status="Solicitud cancelada o tiempo agotado · resultados anteriores conservados";yield break;}
                Status="OpenSees · "+job.status;yield return new WaitForSecondsRealtime(.5f);yield return Http("GET","/jobs/"+job.id,null);
                if(error!=null){Status=error+" · conserva el ID "+job.id+" para consultar/cancelar";yield break;}
                job=JsonUtility.FromJson<Job>(response);
            }
            if(job.status!="completed"){Status=job.status+": "+job.error+" · resultados anteriores conservados";yield break;}
            yield return Http("GET","/jobs/"+job.id+"/delivery",null);if(error!=null){Status=error;yield break;}
            Manifest manifest=JsonUtility.FromJson<Manifest>(response);
            if(manifest.contractVersion!=1||manifest.files==null||string.IsNullOrEmpty(manifest.revision)){Status="Entrega inválida";yield break;}
            string folder=Path.Combine(Application.persistentDataPath,"RemoteJobs",job.id);Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,"delivery.json"),response);
            var names=new HashSet<string>();
            foreach(var file in manifest.files){
                if(Path.GetFileName(file.name)!=file.name||!names.Add(file.name)||file.name.IndexOfAny(new[]{'/', '\\'})>=0){Status="Nombre de archivo inválido";yield break;}
                using(var request=UnityWebRequest.Get(Server.TrimEnd('/')+"/jobs/"+job.id+"/files/"+Uri.EscapeDataString(file.name))){
                    request.SetRequestHeader("Authorization","Bearer "+Token);request.timeout=90;yield return request.SendWebRequest();
                    if(request.result!=UnityWebRequest.Result.Success){Status="Descarga incompleta · resultados anteriores conservados";yield break;}
                    byte[] bytes=request.downloadHandler.data;string hash;
                    using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                    if(hash!=file.sha256){Status="Hash incorrecto · entrega descartada";yield break;}
                    File.WriteAllBytes(Path.Combine(folder,file.name),bytes);
                }
            }
            string[] required=input.operation=="capacity"?new[]{"capacity.json"}:new[]{"model_3d.csv","semana4_resultados.json","semana3_desplazamientos.csv","semana3_esfuerzos_locales.csv","semana5_modelo_hash.txt","estructura_principal.csv","inspeccion_estructural.json"};
            foreach(string name in required)if(!names.Contains(name)){Status="Entrega sin "+name;yield break;}
            if(input.operation=="analysis"&&File.ReadAllText(Path.Combine(folder,"semana5_modelo_hash.txt")).Trim()!=manifest.modelHash){Status="Geometría y manifiesto incompatibles";yield break;}
            if(mine!=generation||cancel){Status="Respuesta antigua descartada";yield break;}
            accept(folder,manifest);Revision=manifest.revision;Status="Revisión validada "+Revision.Substring(0,8);
        } finally {Busy=false;}
    }
    IEnumerator Http(string method,string path,string json){
        error=null;response=null;
        using(var req=new UnityWebRequest(Server.TrimEnd('/')+path,method)){
            req.downloadHandler=new DownloadHandlerBuffer();req.SetRequestHeader("Authorization","Bearer "+Token);req.timeout=15;
            if(json!=null){req.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));req.SetRequestHeader("Content-Type","application/json");}
            yield return req.SendWebRequest();if(req.result!=UnityWebRequest.Result.Success)error="Backend: "+req.responseCode+" "+req.error;else response=req.downloadHandler.text;
        }
    }
    static bool Private(IPAddress ip){byte[] b=ip.GetAddressBytes();return IPAddress.IsLoopback(ip)||(b.Length==4&&(b[0]==10||b[0]==192&&b[1]==168||b[0]==172&&b[1]>=16&&b[1]<=31));}
}
