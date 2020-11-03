using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.RDS;
using Amazon.CDK.AWS.SSM;

namespace NorthwindCdk
{
    public class NorthwindCdkStack : Stack
    {
        internal NorthwindCdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var vpc = new Vpc(this, "LabVpc", new VpcProps
            {
                MaxAzs = 2
            });





            // SQL Server

            var sg = new SecurityGroup(this, "NorthwindDatabaseSecurityGroup", new SecurityGroupProps
            {
                Vpc = vpc,

                SecurityGroupName = "Northwind-DB-SG",
                AllowAllOutbound = false
            });

            // !!!!!!!!!! replace IP according to the instructions above
            sg.AddIngressRule(Peer.Ipv4("35.171.193.180/32"), Port.Tcp(1433)); // SQL Server
            // !!!!!!!!!!

            var sql = new DatabaseInstance(this, "NorthwindSQLServer", new DatabaseInstanceProps
            {
                Vpc = vpc,

                InstanceIdentifier = "northwind-sqlserver",
                Engine = DatabaseInstanceEngine.SqlServerEx(new SqlServerExInstanceEngineProps { Version = SqlServerEngineVersion.VER_14 }), // SQL Server Express

                Credentials = Credentials.FromUsername("adminuser", new CredentialsFromUsernameOptions() {
                    Password = new SecretValue("Admin12345?")
                }),


                //MasterUsername = "adminuser",
                //MasterUserPassword = new SecretValue("Admin12345?"),

                InstanceType = InstanceType.Of(InstanceClass.BURSTABLE3, InstanceSize.SMALL), // t3.small
                SecurityGroups = new ISecurityGroup[] { sg },
                MultiAz = false,
                VpcSubnets = new SubnetSelection() { SubnetType = SubnetType.PUBLIC }, // public subnet

                DeletionProtection = false, // you need to be able to delete database
                DeleteAutomatedBackups = true,
                BackupRetention = Duration.Days(0),
                RemovalPolicy = RemovalPolicy.DESTROY // you need to be able to delete database




            }); ;

            new CfnOutput(this, "SQLServerEndpointAddress", new CfnOutputProps
            {
                Value = sql.DbInstanceEndpointAddress
            });

            // SQL Server connection string in Systems Manager Parameter Store

            new StringParameter(this, "NorthwindDatabaseConnectionString", new StringParameterProps
            {
                ParameterName = "/Northwind/ConnectionStrings/NorthwindDatabase",
                Type = ParameterType.STRING,
                Description = "SQL Server connection string",
                StringValue = string.Format("Server={0},1433;Integrated Security=false;User ID=adminuser;Password=Admin12345?;Initial Catalog=NorthwindTraders;", sql.DbInstanceEndpointAddress)
            });






            // PostgreSQL setup

            // !!!!!!!!!! add 2 rules when you use provided VM, add 1 rule when you use your computer
            sg.AddIngressRule(Peer.Ipv4("35.171.193.180/32"), Port.Tcp(5432)); // PostgreSQL
            sg.AddIngressRule(Peer.Ipv4("3.238.53.13/32"), Port.Tcp(5432)); // PostgreSQL
            // !!!!!!!!!! 

            var postgreSql = new DatabaseCluster(this, "NorthwindPostgreSQL", new DatabaseClusterProps
            {
                InstanceProps = new Amazon.CDK.AWS.RDS.InstanceProps
                {
                    Vpc = vpc,
                    InstanceType = InstanceType.Of(InstanceClass.BURSTABLE3, InstanceSize.MEDIUM), // t3.medium   
                    SecurityGroups = new ISecurityGroup[] { sg },
                    VpcSubnets = new SubnetSelection() { SubnetType = SubnetType.PUBLIC }, // you need to access database from your developer PC
                    ParameterGroup = ParameterGroup.FromParameterGroupName(this, "DBInstanceParameterGroup", "default.aurora-postgresql11"),
                },
                ParameterGroup = ParameterGroup.FromParameterGroupName(this, "DBClusterParameterGroup", "default.aurora-postgresql11"),
                ClusterIdentifier = "northwind-postgresql",
                Engine = DatabaseClusterEngine.AuroraPostgres(new AuroraPostgresClusterEngineProps
                { Version = AuroraPostgresEngineVersion.VER_11_6 }), // Aurora PostgreSQL
                Credentials = Credentials.FromUsername("adminUser", new CredentialsFromUsernameOptions
                {
                    Password = new SecretValue("Admin12345?")
                }),
                //MasterUser = new Login
                //{
                //    Username = "adminuser",
                //    Password = new SecretValue("Admin12345?")
                //},
                Instances = 1,
                Port = 5432,

                Backup = new BackupProps
                {
                    Retention = Duration.Days(1) // minimum is 1
                },

                DefaultDatabaseName = "NorthwindTraders",
                InstanceIdentifierBase = "northwind-postgresql-instance",

                RemovalPolicy = RemovalPolicy.DESTROY // you need to be able to delete database,               
            }); ;

            new CfnOutput(this, "PostgreSQLEndpointAddress", new CfnOutputProps
            {
                Value = postgreSql.ClusterEndpoint.Hostname
            });


            // Aurora PostgreSQL connection string in Systems Manager Parameter Store

            new StringParameter(this, "NorthwindPostgreSQLDatabaseConnectionString", new StringParameterProps
            {
                ParameterName = "/Northwind/ConnectionStrings/NorthwindPostgreDatabase",
                Type = ParameterType.STRING,
                Description = "PostgreSQL connection string",
                StringValue = string.Format("Server={0};Database=NorthwindTraders;Username=adminuser;Password=Admin12345?", postgreSql.ClusterEndpoint.Hostname)
            });







        }
    }
}
