using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.EC2;
using Amazon.EC2.Model;
using System.Configuration;
using Amazon.ElasticLoadBalancing;
using Amazon.ElasticLoadBalancing.Model;
// Add using statements to access AWS SDK for .NET services. 
// Both the Service and its Model namespace need to be added 
// in order to gain access to a service. For example, to access
// the EC2 service, add:
// using Amazon.EC2;
// using Amazon.EC2.Model;

namespace AwsEmptyApp1
{
    class Program
    {
        private static  string terminateLoadBalancerAndInstancesFlag="/terminate";
        public static void Main(string[] args)
        {
            // get access and secret key from project configuration file
            string accessKey=ConfigurationManager.AppSettings["AWSAccessKey"];
            string secretKey=ConfigurationManager.AppSettings["AWSSecretKey"];
            
            // create a config object
            Amazon.Runtime.AWSCredentials conf= new Amazon.Runtime.BasicAWSCredentials(accessKey,secretKey);
            
            // create ec2 client using the config file
            AmazonEC2Client ec2Client = new AmazonEC2Client(conf);

                   
            // create the availability zone based on the user selection
            AvailabilityZone firstAvailabilityZone=null;
            AvailabilityZone secondAvailabilityZone=null;
            chooseAvailabilityZones(ec2Client, ref firstAvailabilityZone, ref secondAvailabilityZone);
            
            // create two Ec2 instance requests
            RunInstancesRequest firstEc2Request = new RunInstancesRequest();
            RunInstancesRequest secondEc2Request = new RunInstancesRequest();

            // specify the parameters of the instances
            firstEc2Request.InstanceType = InstanceType.M1Small;
            secondEc2Request.InstanceType = InstanceType.M1Small;


            firstEc2Request.MinCount = 1;
            firstEc2Request.MaxCount = 1;

            secondEc2Request.MinCount = 1;
            secondEc2Request.MaxCount = 1;

            // check what are the available instances and choose the first that has
            // image id that starts with ami
            DescribeImagesRequest describeImagesRequest = new DescribeImagesRequest();
            Filter filter = new Filter();

            describeImagesRequest.Filters.Add(new Filter() {
                Name = "image-id",
                Values= new List<string>() 
                {
                    "ami*"
                },

                
            });

            DescribeImagesResponse describeImagesResponse = ec2Client.DescribeImages(describeImagesRequest);
        
            string imageId = describeImagesResponse.Images.ElementAt(0).ImageId;
            
            // specify the image id for the instances
            firstEc2Request.ImageId = imageId;
            secondEc2Request.ImageId = imageId;

            // run the two Ec2 instances
            RunInstancesResult runFirstInstanceResult = ec2Client.RunInstances(firstEc2Request);
            RunInstancesResult runSecondInstanceResult = ec2Client.RunInstances(secondEc2Request);

            List<Amazon.EC2.Model.Instance> instancesList = new List<Amazon.EC2.Model.Instance>();
            instancesList.Add(runFirstInstanceResult.Reservation.Instances.ElementAt(0));
            instancesList.Add(runSecondInstanceResult.Reservation.Instances.ElementAt(0));

            // create a registration request for the instances to the load balancer
            RegisterInstancesWithLoadBalancerRequest registerInstancesWithLoadBalancerRequest = new RegisterInstancesWithLoadBalancerRequest();
            
            // add all items to the load balancer instance registration request
            foreach (Amazon.EC2.Model.Instance ec2Instance in instancesList)
            {
                Amazon.ElasticLoadBalancing.Model.Instance ec2InstanceInCorrectFormat = new Amazon.ElasticLoadBalancing.Model.Instance();
                ec2InstanceInCorrectFormat.InstanceId = ec2Instance.InstanceId;
                registerInstancesWithLoadBalancerRequest.Instances.Add(ec2InstanceInCorrectFormat);
            }
            
            // create load balancer request
            CreateLoadBalancerRequest createLoadBalancerRequest = new CreateLoadBalancerRequest();
 
            // add availability zones to the load balancer request
            createLoadBalancerRequest.AvailabilityZones.Add(firstAvailabilityZone.ZoneName);
            createLoadBalancerRequest.AvailabilityZones.Add(secondAvailabilityZone.ZoneName);

            
            createLoadBalancerRequest.LoadBalancerName = "odedLoadBalancer";
            List<Listener> listeners = new List<Listener>();
            createLoadBalancerRequest.Listeners.Add(new Listener("Http", 80, 80));
           
            // create load balancing client
            AmazonElasticLoadBalancingClient loadBalancingClient = new AmazonElasticLoadBalancingClient(conf);
            registerInstancesWithLoadBalancerRequest.LoadBalancerName =createLoadBalancerRequest.LoadBalancerName;

            // create the load balancer
            CreateLoadBalancerResult result = loadBalancingClient.CreateLoadBalancer(createLoadBalancerRequest);

            // pass the register instances request to the load balancer
            loadBalancingClient.RegisterInstancesWithLoadBalancer(registerInstancesWithLoadBalancerRequest);

            // if the command line argument have / remove the instances and the load balancer
            if(args[0].Equals(terminateLoadBalancerAndInstancesFlag)){
                TerminateInstancesRequest terminateInstancesRequest = new TerminateInstancesRequest();
                terminateInstancesRequest.InstanceIds.Add(runFirstInstanceResult.Reservation.Instances.ElementAt(0).InstanceId);
                terminateInstancesRequest.InstanceIds.Add(runSecondInstanceResult.Reservation.Instances.ElementAt(0).InstanceId);
                TerminateInstancesResponse terminateInstancesResponse = ec2Client.TerminateInstances(terminateInstancesRequest);
                DeleteLoadBalancerResponse deleteLoadBalancerResponse = loadBalancingClient.DeleteLoadBalancer(new DeleteLoadBalancerRequest() {
                    LoadBalancerName=createLoadBalancerRequest.LoadBalancerName 
                });

            }


        }










        private static void chooseAvailabilityZones(AmazonEC2Client ec2Client, ref AvailabilityZone firstAvailabilityZone, ref AvailabilityZone secondAvailabilityZone)
        {
            // allow user to choose availability zones for the ec2 instances
            //Console.Out.WriteLine("Please choose availability zone:");
            DescribeAvailabilityZonesResponse availabilityZoneresponse = ec2Client.DescribeAvailabilityZones();
            int index = 1;
            foreach (AvailabilityZone zone in availabilityZoneresponse.AvailabilityZones)
            {
                Console.Out.WriteLine(index++ + ")" + zone.ZoneName);
            }
            int selectionFirst = 0;
            int selectionSecond = 0;
            bool start = true;

            while (selectionFirst == selectionSecond || selectionFirst < 1 || selectionFirst > index || selectionSecond < 1 || selectionSecond > index)
            {

                if (start)
                {
                    start = false;
                }
                else
                {
                    Console.Out.WriteLine("Bad seletion please enter again");
                }
                Console.Out.WriteLine("choose index of first:");
                if (!int.TryParse(Console.ReadLine(), out selectionFirst))
                {
                    continue;
                }
                Console.Out.WriteLine("choose index of second:");
                if (!int.TryParse(Console.ReadLine(), out selectionSecond))
                {
                    continue;
                }

            }
            firstAvailabilityZone = availabilityZoneresponse.AvailabilityZones.ElementAt(selectionFirst);
            secondAvailabilityZone = availabilityZoneresponse.AvailabilityZones.ElementAt(selectionSecond);
        }
    }
}