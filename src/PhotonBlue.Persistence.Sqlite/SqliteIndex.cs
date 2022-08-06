using Microsoft.Data.Sqlite;

namespace PhotonBlue.Persistence.Sqlite;

public class SqliteIndex : IGameFileIndex
{
    private readonly string _connectionString;

    public SqliteIndex(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IEnumerable<IndexRepository> GetAllRepositories()
    {
        using var conn = new SqliteConnection(_connectionString);
        using var command = conn.CreateCommand();
        command.CommandText = @"SELECT * FROM repository";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return new IndexRepository
            {
                Name = reader.GetString(0),
            };
        }
    }

    public IndexRepository? GetRepository(string name)
    {
        using var conn = new SqliteConnection(_connectionString);
        using var command = conn.CreateCommand();
        command.CommandText = @"SELECT * FROM repository WHERE name = $1";
        command.Parameters.Add(name, SqliteType.Text);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new IndexRepository
        {
            Name = reader.GetString(0),
        };
    }

    public IEnumerable<IndexPack> GetAllPacks()
    {
        using var conn = new SqliteConnection(_connectionString);
        using var command = conn.CreateCommand();
        command.CommandText = @"SELECT * FROM pack";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return new IndexPack
            {
                Hash = reader.GetString(0),
                Repository = reader.GetString(1),
                UpdatedAt = reader.GetDateTime(2),
            };
        }
    }

    public IndexPack? GetPack(string hash)
    {
        using var conn = new SqliteConnection(_connectionString);
        using var command = conn.CreateCommand();
        command.CommandText = @"SELECT * FROM pack WHERE hash = $1";
        command.Parameters.Add(hash, SqliteType.Text);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new IndexPack
        {
            Hash = reader.GetString(0),
            Repository = reader.GetString(1),
            UpdatedAt = reader.GetDateTime(2),
        };
    }

    public IEnumerable<IndexFileEntry> GetAllFileEntries()
    {
        using var conn = new SqliteConnection(_connectionString);
        using var command = conn.CreateCommand();
        command.CommandText = @"SELECT * FROM file_entry";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return new IndexFileEntry()
            {
                PackHash = reader.GetString(0),
                FileName = reader.GetString(1),
            };
        }
    }

    public IEnumerable<IndexFileEntry> GetAllFileEntries(string fileName)
    {
        using var conn = new SqliteConnection(_connectionString);
        using var command = conn.CreateCommand();
        command.CommandText = @"SELECT * FROM file_entry WHERE file_name = $1";
        command.Parameters.Add(fileName, SqliteType.Text);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return new IndexFileEntry()
            {
                PackHash = reader.GetString(0),
                FileName = reader.GetString(1),
            };
        }
    }

    public IndexFileEntry? GetFileEntry(string packHash, string fileName)
    {
        using var conn = new SqliteConnection(_connectionString);
        using var command = conn.CreateCommand();
        command.CommandText = @"SELECT * FROM file_entry WHERE pack_hash = $1 AND file_name = $2";
        command.Parameters.Add(packHash, SqliteType.Text);
        command.Parameters.Add(fileName, SqliteType.Text);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new IndexFileEntry()
        {
            PackHash = reader.GetString(0),
            FileName = reader.GetString(1),
        };
    }

    public void StoreRepository(IndexRepository repository)
    {
        throw new NotImplementedException();
    }

    public void StorePack(IndexPack pack)
    {
        throw new NotImplementedException();
    }

    public void StoreFileEntry(IndexFileEntry entry)
    {
        throw new NotImplementedException();
    }

    public void UpdateRepository(IndexRepository repository)
    {
        throw new NotImplementedException();
    }

    public void UpdatePack(IndexPack pack)
    {
        throw new NotImplementedException();
    }

    public void UpdateFileEntry(IndexFileEntry entry)
    {
        throw new NotImplementedException();
    }

    public void DeleteRepository(IndexRepository repository)
    {
        throw new NotImplementedException();
    }

    public void DeletePack(IndexPack pack)
    {
        throw new NotImplementedException();
    }

    public void DeleteFileEntry(IndexFileEntry entry)
    {
        throw new NotImplementedException();
    }
}