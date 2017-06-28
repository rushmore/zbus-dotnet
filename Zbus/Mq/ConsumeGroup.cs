using System;
using System.Threading.Tasks;
using Zbus.Mq.Net;

namespace Zbus.Mq
{
    public class ConsumeGroup
    { 
        public string GroupName { get; set; }
        public string Filter { get; set; } //message in group is filtered
        public int? Mask { get; set; }
        public string Creator { get; set; }
        public string StartCopy { get; set; }//create group from another group 
        public long? StartOffset { get; set; }//create group start from offset, msgId to check valid
        public string StartMsgId { get; set; } 
        public long? StartTime { get; set; }//create group start from time 

        public ConsumeGroup()
        {

        }
        public ConsumeGroup(string groupName)
        {
            this.GroupName = groupName;
        }
    }
}
