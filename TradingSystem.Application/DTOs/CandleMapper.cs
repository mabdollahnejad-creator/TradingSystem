using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingSystem.Domain.Entities;

namespace TradingSystem.Application.DTOs;
public static class CandleMapper
{
    public static CandleDto ToDto(Candle entity)
    {
        return new CandleDto
        {
            OpenTime = entity.OpenTime,
            Open = entity.Open,
            High = entity.High,
            Low = entity.Low,
            Close = entity.Close,
            Volume = entity.Volume
        };
    }

    public static List<CandleDto> ToDtoList(IEnumerable<Candle> entities)
    {
        return entities.Select(ToDto).ToList();
    }
}
