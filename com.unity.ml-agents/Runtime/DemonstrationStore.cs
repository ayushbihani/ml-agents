using System.IO;
using System.IO.Abstractions;
using Google.Protobuf;
using System.Collections.Generic;
using MLAgents.Sensor;

namespace MLAgents
{
    /// <summary>
    /// Responsible for writing demonstration data to file.
    /// </summary>
    public class DemonstrationStore
    {
        public const int MetaDataBytes = 32; // Number of bytes allocated to metadata in demo file.
        readonly IFileSystem m_FileSystem;
        const string k_DemoDirectory = "Assets/Demonstrations/";
        const string k_ExtensionType = ".demo";

        DemonstrationMetaData m_MetaData;
        Stream m_Writer;
        float m_CumulativeReward;
        WriteAdapter m_WriteAdapter = new WriteAdapter();

        public DemonstrationStore(IFileSystem fileSystem)
        {
            if (fileSystem != null)
            {
                m_FileSystem = fileSystem;
            }
            else
            {
                m_FileSystem = new FileSystem();
            }
        }

        /// <summary>
        /// Initializes the Demonstration Store, and writes initial data.
        /// </summary>
        public void Initialize(
            string demonstrationName, BrainParameters brainParameters, string brainName)
        {
            CreateDirectory();
            CreateDemonstrationFile(demonstrationName);
            WriteBrainParameters(brainName, brainParameters);
        }

        /// <summary>
        /// Checks for the existence of the Demonstrations directory
        /// and creates it if it does not exist.
        /// </summary>
        void CreateDirectory()
        {
            if (!m_FileSystem.Directory.Exists(k_DemoDirectory))
            {
                m_FileSystem.Directory.CreateDirectory(k_DemoDirectory);
            }
        }

        /// <summary>
        /// Creates demonstration file.
        /// </summary>
        void CreateDemonstrationFile(string demonstrationName)
        {
            // Creates demonstration file.
            var literalName = demonstrationName;
            var filePath = k_DemoDirectory + literalName + k_ExtensionType;
            var uniqueNameCounter = 0;
            while (m_FileSystem.File.Exists(filePath))
            {
                literalName = demonstrationName + "_" + uniqueNameCounter;
                filePath = k_DemoDirectory + literalName + k_ExtensionType;
                uniqueNameCounter++;
            }

            m_Writer = m_FileSystem.File.Create(filePath);
            m_MetaData = new DemonstrationMetaData { demonstrationName = demonstrationName };
            var metaProto = m_MetaData.ToProto();
            metaProto.WriteDelimitedTo(m_Writer);
        }

        /// <summary>
        /// Writes brain parameters to file.
        /// </summary>
        void WriteBrainParameters(string brainName, BrainParameters brainParameters)
        {
            // Writes BrainParameters to file.
            m_Writer.Seek(MetaDataBytes + 1, 0);
            var brainProto = brainParameters.ToProto(brainName, false);
            brainProto.WriteDelimitedTo(m_Writer);
        }

        /// <summary>
        /// Write AgentInfo experience to file.
        /// </summary>
        public void Record(AgentInfo info, List<ISensor> sensors)
        {
            // Increment meta-data counters.
            m_MetaData.numberExperiences++;
            m_CumulativeReward += info.reward;
            if (info.done)
            {
                EndEpisode();
            }

            // Generate observations and add AgentInfo to file.
            var agentProto = info.ToInfoActionPairProto();
            foreach (var sensor in sensors)
            {
                agentProto.AgentInfo.Observations.Add(sensor.GetObservationProto(m_WriteAdapter));
            }

            agentProto.WriteDelimitedTo(m_Writer);
        }

        /// <summary>
        /// Performs all clean-up necessary
        /// </summary>
        public void Close()
        {
            EndEpisode();
            m_MetaData.meanReward = m_CumulativeReward / m_MetaData.numberEpisodes;
            WriteMetadata();
            m_Writer.Close();
        }

        /// <summary>
        /// Performs necessary episode-completion steps.
        /// </summary>
        void EndEpisode()
        {
            m_MetaData.numberEpisodes += 1;
        }

        /// <summary>
        /// Writes meta-data.
        /// </summary>
        void WriteMetadata()
        {
            var metaProto = m_MetaData.ToProto();
            var metaProtoBytes = metaProto.ToByteArray();
            m_Writer.Write(metaProtoBytes, 0, metaProtoBytes.Length);
            m_Writer.Seek(0, 0);
            metaProto.WriteDelimitedTo(m_Writer);
        }
    }
}
