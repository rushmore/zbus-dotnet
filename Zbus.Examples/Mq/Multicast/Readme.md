## Multicast Demo
Run ConsumeExample instances(assume named as Consumer.exe) with different ConsumeGroups, but in eah group start more than one instances.

    Consumer.exe  -g MulticastGroup1 -t MyTopic
    Consumer.exe  -g MulticastGroup1 -t MyTopic

    Consumer.exe  -g MulticastGroup2 -t MyTopic
    Consumer.exe  -g MulticastGroup2 -t MyTopic

After the setup, any message produced to MyTopic will be avaiable in both groups(MulticastGroup1 and MulticastGroup1), but every instance in one group should be loadbalancing each other.