namespace TMIAutomation.Runner.Persistence
{
    internal interface IDataRepository
    {
        Data Load();
        void Save(Data data);
    }
}
