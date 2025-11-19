namespace Nivtropy.Models
{
    /// <summary>
    /// Режим раскраски строк в таблице
    /// </summary>
    public enum RowColoringMode
    {
        /// <summary>
        /// Без раскраски
        /// </summary>
        None = 0,

        /// <summary>
        /// Чередующиеся цвета строк
        /// </summary>
        Alternating = 1,

        /// <summary>
        /// Градиент - чем ниже строка в ходе, тем темнее
        /// </summary>
        Gradient = 2
    }
}
