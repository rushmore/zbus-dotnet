## Broadcast Demo
Run ConsumeExample instances(assume named as Consumer.exe) with different ConsumeGroups

    Consumer.exe  -g BroadcastGroup1 -t MyTopic
    Consumer.exe  -g BroadcastGroup2 -t MyTopic
    Consumer.exe  -g BroadcastGroup3 -t MyTopic

You may run as many consumers as you can, but each of the consumer should be started with different group and same topic.

For Hight Availability, you may specifiy broker lists, -h for more configuration details.

For producers, there is no requirement, any message produced to MyTopic should be broadcasted to all the consumers.