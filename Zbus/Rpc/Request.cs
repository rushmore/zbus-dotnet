namespace Zbus.Rpc
{
    /// <summary>
    /// Method+Params stands for a Rpc request.
    /// 
    /// To support hierachy
    /// ServiceId:Module:Method if the full reference of a method.
    /// By default both ServiceId and Module default to null. 
    /// 
    /// </summary>
    public class Request
    {
        public string Method { get; set; }
        public object[] Params { get; set; } 
        public string Module { get; set; }//optional 
    }
}