using System.Collections.Generic;

namespace StardewModdingAPI.Framework.StateTracking.FieldWatchers;

/// <summary>A watcher which detects changes to a collection of values using a specified <see cref="IEqualityComparer{T}"/> instance.</summary>
/// <typeparam name="TValue">The value type within the collection.</typeparam>
internal class ComparableListWatcher<TValue> : BaseDisposableWatcher, ICollectionWatcher<TValue>
{
    /*********
    ** Fields
    *********/
    /// <summary>The collection to watch.</summary>
    private readonly ICollection<TValue> CurrentValues;

    /// <summary>The values during the previous update.</summary>
    private HashSet<TValue> LastValues;

    /// <summary>Working buffer for current values, swapped with <see cref="LastValues"/> each update.</summary>
    private HashSet<TValue> WorkingBuffer;

    /// <summary>The pairs added since the last reset.</summary>
    private readonly List<TValue> AddedImpl = [];

    /// <summary>The pairs removed since the last reset.</summary>
    private readonly List<TValue> RemovedImpl = [];


    /*********
    ** Accessors
    *********/
    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public bool IsChanged => this.AddedImpl.Count > 0 || this.RemovedImpl.Count > 0;

    /// <inheritdoc />
    public IEnumerable<TValue> Added => this.AddedImpl;

    /// <inheritdoc />
    public IEnumerable<TValue> Removed => this.RemovedImpl;


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="name">A name which identifies what the watcher is watching, used for troubleshooting.</param>
    /// <param name="values">The collection to watch.</param>
    /// <param name="comparer">The equality comparer which indicates whether two values are the same.</param>
    public ComparableListWatcher(string name, ICollection<TValue> values, IEqualityComparer<TValue> comparer)
    {
        this.Name = name;
        this.CurrentValues = values;
        this.LastValues = new HashSet<TValue>(comparer);
        this.WorkingBuffer = new HashSet<TValue>(comparer);
    }

    /// <inheritdoc />
    public void Update()
    {
        this.AssertNotDisposed();

        // optimize for zero items
        if (this.CurrentValues.Count == 0)
        {
            if (this.LastValues.Count > 0)
            {
                this.RemovedImpl.AddRange(this.LastValues);
                this.LastValues.Clear();
            }
            return;
        }

        // detect changes using double-buffer pattern
        this.WorkingBuffer.Clear();
        foreach (TValue item in this.CurrentValues)
            this.WorkingBuffer.Add(item);

        foreach (TValue value in this.LastValues)
        {
            if (!this.WorkingBuffer.Contains(value))
                this.RemovedImpl.Add(value);
        }
        foreach (TValue value in this.WorkingBuffer)
        {
            if (!this.LastValues.Contains(value))
                this.AddedImpl.Add(value);
        }

        // swap buffers
        (this.LastValues, this.WorkingBuffer) = (this.WorkingBuffer, this.LastValues);
    }

    /// <inheritdoc />
    public void Reset()
    {
        this.AssertNotDisposed();

        this.AddedImpl.Clear();
        this.RemovedImpl.Clear();
    }
}
