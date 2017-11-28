﻿using Amazon.Common.DotNetCli.Tools;
using Amazon.ECS.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThirdParty.Json.LitJson;

namespace Amazon.ECS.Tools.Commands
{
    public class ECSTaskDefinitionUtilities
    {
        public static async Task<string> CreateOrUpdateTaskDefinition(IToolLogger logger, IAmazonECS ecsClient, ECSBaseCommand command, 
            TaskDefinitionProperties properties, string dockerImageTag, bool isFargate)
        {
            var ecsContainer = command.GetStringValueOrDefault(properties.ContainerName, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_NAME, true);
            var ecsTaskDefinition = command.GetStringValueOrDefault(properties.TaskDefinitionName, ECSDefinedCommandOptions.ARGUMENT_TD_NAME, true);

            try
            {
                DescribeTaskDefinitionResponse response = null;
                try
                {
                    response = await ecsClient.DescribeTaskDefinitionAsync(new DescribeTaskDefinitionRequest
                    {
                        TaskDefinition = ecsTaskDefinition
                    });
                }
                catch (Exception e)
                {
                    if (!(e is ClientException))
                    {
                        throw;
                    }
                }

                var registerRequest = new RegisterTaskDefinitionRequest()
                {
                    Family = ecsTaskDefinition
                };

                if (response == null || response.TaskDefinition == null)
                {
                    logger?.WriteLine("Creating new task definition");
                }
                else
                {
                    logger?.WriteLine("Updating existing task definition");

                    registerRequest.ContainerDefinitions = response.TaskDefinition.ContainerDefinitions;
                    registerRequest.Cpu = response.TaskDefinition.Cpu;
                    registerRequest.ExecutionRoleArn = response.TaskDefinition.ExecutionRoleArn;
                    registerRequest.Memory = response.TaskDefinition.Memory;
                    registerRequest.NetworkMode = response.TaskDefinition.NetworkMode;
                    registerRequest.PlacementConstraints = response.TaskDefinition.PlacementConstraints;
                    registerRequest.RequiresCompatibilities = response.TaskDefinition.RequiresCompatibilities;
                    registerRequest.TaskRoleArn = response.TaskDefinition.TaskRoleArn;
                    registerRequest.Volumes = response.TaskDefinition.Volumes;
                }

                var networkMode = command.GetStringValueOrDefault(properties.TaskDefinitionNetworkMode, ECSDefinedCommandOptions.ARGUMENT_TD_NETWORK_MODE, false);
                if (!string.IsNullOrEmpty(networkMode))
                {
                    registerRequest.NetworkMode = networkMode;
                }

                {
                    JsonData volumes = command.GetJsonValueOrDefault(properties.TaskDefinitionVolumes, ECSDefinedCommandOptions.ARGUMENT_TD_VOLUMES);
                    if (volumes != null)
                    {
                        foreach(JsonData item in volumes)
                        {
                            var volume = new Volume();

                            if(item["host"] != null)
                            {
                                volume.Host = new HostVolumeProperties();
                                volume.Host.SourcePath = item["host"]["sourcePath"] != null ? item["host"]["sourcePath"].ToString() : null;
                            }
                            volume.Name = item["name"] != null ? item["name"].ToString() : null;
                        }
                    }
                }

                var taskIAMRole = command.GetStringValueOrDefault(properties.TaskDefinitionRole, ECSDefinedCommandOptions.ARGUMENT_TD_ROLE, false);
                if (!string.IsNullOrWhiteSpace(taskIAMRole))
                {
                    registerRequest.TaskRoleArn = taskIAMRole;
                }

                var containerDefinition = registerRequest.ContainerDefinitions.FirstOrDefault(x => string.Equals(x.Name, ecsContainer, StringComparison.Ordinal));

                if (containerDefinition == null)
                {
                    logger?.WriteLine("Creating new container definition");

                    containerDefinition = new ContainerDefinition
                    {
                        Name = ecsContainer
                    };
                    registerRequest.ContainerDefinitions.Add(containerDefinition);
                }

                containerDefinition.Image = dockerImageTag;

                {
                    var containerCommands = command.GetStringValuesOrDefault(properties.ContainerCommands, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_COMMANDS, false);
                    if(containerCommands != null && containerCommands.Length > 0)
                    {
                        containerDefinition.Command = new List<string>(containerCommands);
                    }
                }
                {
                    var disableNetworking = command.GetBoolValueOrDefault(properties.ContainerDisableNetworking, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_DISABLE_NETWORKING, false);
                    if(disableNetworking.HasValue)
                    {
                        containerDefinition.DisableNetworking = disableNetworking.Value;
                    }
                }
                {
                    var dnsSearchServers = command.GetStringValuesOrDefault(properties.ContainerDNSSearchDomains, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_DNS_SEARCH_DOMAINS, false);
                    if(dnsSearchServers != null && dnsSearchServers.Length > 0)
                    {
                        containerDefinition.DnsSearchDomains = new List<string>(dnsSearchServers);
                    }
                }
                {
                    var dnsServers = command.GetStringValuesOrDefault(properties.ContainerDNSServers, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_DNS_SERVERS, false);
                    if (dnsServers != null && dnsServers.Length > 0)
                    {
                        containerDefinition.DnsServers = new List<string>(dnsServers);
                    }
                }
                {
                    var labels = command.GetKeyValuePairOrDefault(properties.ContainerDockerLabels, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_DOCKER_LABELS, false);
                    if (labels != null && labels.Count > 0)
                    {
                        containerDefinition.DockerLabels = new Dictionary<string, string>(labels);
                    }
                }
                {
                    var options = command.GetStringValuesOrDefault(properties.ContainerDockerSecurityOptions, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_DOCKER_SECURITY_OPTIONS, false);
                    if (options != null && options.Length > 0)
                    {
                        containerDefinition.DockerSecurityOptions = new List<string>(options);
                    }
                }
                {
                    var entrypoints = command.GetStringValuesOrDefault(properties.ContainerEntryPoint, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_ENTRY_POINT, false);
                    if (entrypoints != null && entrypoints.Length > 0)
                    {
                        containerDefinition.EntryPoint = new List<string>(entrypoints);
                    }
                }
                {
                    var environmentVariables = command.GetKeyValuePairOrDefault(properties.ContainerEnvironmentVariables, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_ENVIRONMENT_VARIABLES, false);
                    if (environmentVariables != null && environmentVariables.Count > 0)
                    {
                        var listEnv = new List<Amazon.ECS.Model.KeyValuePair>();
                        foreach (var e in environmentVariables)
                        {
                            listEnv.Add(new Amazon.ECS.Model.KeyValuePair { Name = e.Key, Value = e.Value });
                        }
                        containerDefinition.Environment = listEnv;
                    }
                }
                {
                    var essential = command.GetBoolValueOrDefault(properties.ContainerEssential, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_ESSENTIAL, false);
                    if(essential.HasValue)
                    {
                        containerDefinition.Essential = essential.Value;
                    }
                }
                {
                    JsonData data = command.GetJsonValueOrDefault(properties.ContainerExtraHosts, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_EXTRA_HOSTS);
                    if (data != null)
                    {
                        containerDefinition.ExtraHosts.Clear();
                        foreach (JsonData item in data)
                        {
                            var obj = new Amazon.ECS.Model.HostEntry();

                            obj.Hostname = item["hostname"] != null ? item["hostname"].ToString() : null;
                            obj.IpAddress = item["ipAddress"] != null ? item["ipAddress"].ToString() : null;
                            containerDefinition.ExtraHosts.Add(obj);
                        }
                    }
                }
                {
                    var hostname = command.GetStringValueOrDefault(properties.ContainerHostname, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_HOSTNAME, false);
                    if(!string.IsNullOrEmpty(hostname))
                    {
                        containerDefinition.Hostname = hostname;
                    }
                }
                {
                    var links = command.GetStringValuesOrDefault(properties.ContainerLinks, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_LINKS, false);
                    if (links != null && links.Length > 0)
                    {
                        containerDefinition.Links = new List<string>(links);
                    }
                }
                {
                    JsonData data = command.GetJsonValueOrDefault(properties.ContainerExtraHosts, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_EXTRA_HOSTS);
                    if (data != null)
                    {
                        var linuxParameter = new LinuxParameters();

                        if(data["capabilities"] != null)
                        {
                            linuxParameter.Capabilities = new KernelCapabilities();
                            if(data["capabilities"]["drop"] != null)
                            {
                                foreach(var item in data["capabilities"]["drop"])
                                {
                                    linuxParameter.Capabilities.Drop.Add(item.ToString());
                                }
                            }
                            if (data["capabilities"]["add"] != null)
                            {
                                foreach (var item in data["capabilities"]["add"])
                                {
                                    linuxParameter.Capabilities.Add.Add(item.ToString());
                                }
                            }
                        }
                        if (data["devices"] != null)
                        {
                            linuxParameter.Devices.Clear();
                            foreach(JsonData item in data["devices"])
                            {
                                var device = new Device();
                                device.ContainerPath = item["containerPath"] != null ? item["containerPath"].ToString() : null;
                                device.HostPath = item["hostPath"] != null ? item["hostPath"].ToString() : null;
                                foreach(string permission in item["permissions"])
                                {
                                    device.Permissions.Add(permission);
                                }

                                linuxParameter.Devices.Add(device);
                            }
                        }

                        if (data["initProcessEnabled"] != null && data["initProcessEnabled"].IsBoolean)
                            linuxParameter.InitProcessEnabled = (bool)data["initProcessEnabled"];

                        containerDefinition.LinuxParameters = linuxParameter;
                    }
                }
                {
                    JsonData data = command.GetJsonValueOrDefault(properties.ContainerLogConfiguration, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_LOG_CONFIGURATION);
                    if (data != null)
                    {
                        containerDefinition.LogConfiguration = new LogConfiguration();
                        containerDefinition.LogConfiguration.LogDriver = data["containerPath"] != null ? data["containerPath"].ToString() : null;
                        if (data["options"] != null)
                        {
                            foreach (var key in data["options"].PropertyNames)
                            {
                                containerDefinition.LogConfiguration.Options[key] = data["options"][key].ToString();
                            }
                        }
                    }
                }
                {
                    var hardLimit = command.GetIntValueOrDefault(properties.ContainerMemoryHardLimit, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_MEMORY_HARD_LIMIT, false);
                    if (hardLimit.HasValue)
                    {
                        logger?.WriteLine($"Setting container hard memory limit {hardLimit.Value}MiB");
                        containerDefinition.Memory = hardLimit.Value;
                    }
                }
                {
                    var softLimit = command.GetIntValueOrDefault(properties.ContainerMemorySoftLimit, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_MEMORY_SOFT_LIMIT, false);
                    if (softLimit.HasValue)
                    {
                        logger?.WriteLine($"Setting container soft memory limit {softLimit.Value}MiB");
                        containerDefinition.MemoryReservation = softLimit.Value;
                    }
                }
                {
                    JsonData data = command.GetJsonValueOrDefault(properties.ContainerMountPoints, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_MOUNT_POINTS);
                    if(data != null)
                    {
                        containerDefinition.MountPoints.Clear();
                        foreach(JsonData item in data)
                        {
                            var mountPoint = new MountPoint();
                            mountPoint.ContainerPath = item["containerPath"] != null ? item["containerPath"].ToString() : null;
                            mountPoint.ContainerPath = item["sourceVolume"] != null ? item["sourceVolume"].ToString() : null;
                            if(item["readOnly"] != null && item["readOnly"].IsBoolean)
                            {
                                mountPoint.ReadOnly = (bool)item["readOnly"];
                            }

                            containerDefinition.MountPoints.Add(mountPoint);
                        }
                    }
                }
                {
                    var portMappings = command.GetStringValuesOrDefault(properties.ContainerPortMappings, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_PORT_MAPPING, false);
                    if (portMappings != null)
                    {
                        containerDefinition.PortMappings = new List<PortMapping>();
                        foreach (var mapping in portMappings)
                        {
                            var tokens = mapping.Split(':');
                            if (tokens.Length != 2)
                            {
                                throw new DockerToolsException($"Port mapping {mapping} is invalid. Format should be <host-port>:<container-port>,<host-port>:<container-port>,...", DockerToolsException.CommonErrorCode.CommandLineParseError);
                            }

                            logger?.WriteLine($"Adding port mapping host {tokens[0]} to container {tokens[1]}");
                            containerDefinition.PortMappings.Add(new PortMapping
                            {
                                HostPort = int.Parse(tokens[0]),
                                ContainerPort = int.Parse(tokens[1])
                            });
                        }
                    }
                }
                {
                    var privileged = command.GetBoolValueOrDefault(properties.ContainerPrivileged, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_PRIVILEGED, false);
                    if (privileged.HasValue)
                    {
                        containerDefinition.Privileged = privileged.Value;
                    }
                }
                {
                    var readonlyFilesystem = command.GetBoolValueOrDefault(properties.ContainerReadonlyRootFilesystem, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_READONLY_ROOT_FILESYSTEM, false);
                    if (readonlyFilesystem.HasValue)
                    {
                        containerDefinition.ReadonlyRootFilesystem = readonlyFilesystem.Value;
                    }
                }
                {
                    JsonData data = command.GetJsonValueOrDefault(properties.ContainerUlimits, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_ULIMITS);
                    if(data != null)
                    {
                        containerDefinition.Ulimits.Clear();
                        foreach(JsonData item in data)
                        {
                            var ulimit = new Ulimit();

                            ulimit.Name = item["name"] != null ? item["name"].ToString() : null;
                            if(item["hardLimit"] != null && item["hardLimit"].IsInt)
                            {
                                ulimit.HardLimit = (int)item["hardLimit"];
                            }
                            if (item["softLimit"] != null && item["softLimit"].IsInt)
                            {
                                ulimit.HardLimit = (int)item["softLimit"];
                            }

                            containerDefinition.Ulimits.Add(ulimit);
                        }
                    }
                }
                {
                    var containerUser = command.GetStringValueOrDefault(properties.ContainerUser, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_USER, false);
                    if (!string.IsNullOrEmpty(containerUser))
                    {
                        containerDefinition.User = containerUser;
                    }
                }
                {
                    JsonData data = command.GetJsonValueOrDefault(properties.ContainerVolumesFrom, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_VOLUMES_FROM);
                    if (data != null)
                    {
                        containerDefinition.VolumesFrom.Clear();
                        foreach(JsonData item in data)
                        {
                            var volume = new VolumeFrom();
                            volume.SourceContainer = item["sourceContainer"] != null ? item["sourceContainer"].ToString() : null;
                            if (item["readOnly"] != null && item["readOnly"].IsBoolean)
                            {
                                volume.ReadOnly = (bool)item["readOnly"];
                            }

                            containerDefinition.VolumesFrom.Add(volume);
                        }
                    }
                }
                {
                    var workingDirectory = command.GetStringValueOrDefault(properties.ContainerWorkingDirectory, ECSDefinedCommandOptions.ARGUMENT_CONTAINER_WORKING_DIRECTORY, false);
                    if (!string.IsNullOrEmpty(workingDirectory))
                    {
                        containerDefinition.WorkingDirectory = workingDirectory;
                    }
                }


                if(isFargate)
                {
                    if(registerRequest.NetworkMode != null && registerRequest.NetworkMode != NetworkMode.Awsvpc)
                    {
                        logger?.WriteLine("Setting metwork mode to \"awsvpc\" which is required to launch fargate based tasks");
                    }

                    registerRequest.NetworkMode = NetworkMode.Awsvpc;

                    if (registerRequest.RequiresCompatibilities == null)
                        registerRequest.RequiresCompatibilities = new List<string>();

                    if (!registerRequest.RequiresCompatibilities.Contains("FARGATE"))
                        registerRequest.RequiresCompatibilities.Add("FARGATE");

                    registerRequest.Memory = command.GetStringValueOrDefault(properties.TaskMemory, ECSDefinedCommandOptions.ARGUMENT_TD_MEMORY, true);
                    registerRequest.Cpu = command.GetStringValueOrDefault(properties.TaskCPU, ECSDefinedCommandOptions.ARGUMENT_TD_CPU, true);

                    registerRequest.ExecutionRoleArn = command.GetStringValueOrDefault(properties.TaskDefinitionExecutionRole, ECSDefinedCommandOptions.ARGUMENT_TD_EXECUTION_ROLE, true);
                }
                else
                {
                    registerRequest.ExecutionRoleArn = null;
                }

                if (registerRequest.NetworkMode != NetworkMode.Awsvpc)
                {
                    registerRequest.RequiresCompatibilities.Clear();
                }

                var registerResponse = await ecsClient.RegisterTaskDefinitionAsync(registerRequest);
                logger?.WriteLine($"Registered new task definition revision {registerResponse.TaskDefinition.Revision}");
                return registerResponse.TaskDefinition.TaskDefinitionArn;
            }
            catch (DockerToolsException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new DockerToolsException($"Error updating ECS task definition {ecsTaskDefinition}: {e.Message}", DockerToolsException.ECSErrorCode.FailedToUpdateTaskDefinition);
            }
        }
    }
}