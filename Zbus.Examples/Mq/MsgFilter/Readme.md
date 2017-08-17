## Message Filter Demo

Message filter is another useful concept for message filter in the server side. Message filter applies to the ConsumeGroup wide, which means all the consumers in the same group are all affected by the message filter. As you can see from Multicast and Broadcast examples, consume-groups make Unicast/Multicast and Broadcast messaging modules all possible, message filter, on the other hand, filters the message by its *tag* property, and when combined with these messaging models, they can form even more complicated messaging models.

Run ConsumeExample instances(assume named as Consumer.exe) with filter enabled. 

    Consumer.exe  -g FilterGroup1 -t MyTopic - f "Stock.A.*"            ----(1)
    Consumer.exe  -g FilterGroup1 -t MyTopic - f "Stock.A.*"            ----(2)

    Consumer.exe  -g FilterGroup2 -t MyTopic - f "Stock.HK.*"           ----(3)


Instance (1) and (2) only consumes message with **tag** starts with *Stock.A.*, but (1) and (2) still follow the principle of loadbalancing in one Group(FilterGroup1)

Instance (3), however only consumes the message with **tag** starts with *Stock.HK.*.

In case you do want each consumer subscribe on some specific domain(*tag*, to differentiate from Topic already used in zbus), you can start instances with distinct consume-groups and of each with distinct **filter**